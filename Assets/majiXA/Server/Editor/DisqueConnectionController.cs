using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using DisquuunCore;
using DisquuunCore.Deserialize;
using System.Collections.Concurrent;

namespace majiXA
{
    public class DisqueConnectionController
    {
        private ServerContext context;
        private Disquuun disquuun;
        private DisqueReconnector disqueReconnector;

        public DisqueConnectionController(string contextQueueIdentity, string ip, int port)
        {
            disqueReconnector = new DisqueReconnector(contextQueueIdentity, ip, port);
            Connect(contextQueueIdentity, ip, port);
        }

        public void Connect(string contextQueueIdentity, string ip, int port)
        {
            disquuun = new Disquuun(
                ip,
                port,
                1024 * 100,
                30,
                conId => {
                    // enable adding job to Disque by swapping publisher method.
                    //context.Setup(Publish);

                    // start getting job from disque then fastack all automatically.
                    disquuun.GetJob(new string[] { contextQueueIdentity }, "count", 10000).Loop(
                        (command, data) => {
                            var jobs = DisquuunDeserializer.GetJob(data);

                            var jobIds = jobs.Select(jobData => jobData.jobId).ToArray();
                            var jobDatas = jobs.Select(jobData => jobData.jobData).ToList();

                            /*
                                fast ack all.
                            */
                            disquuun.FastAck(jobIds).Async((command2, data2) => { });

                            InputDatasToContext(jobDatas);
                            return true;
                        }
                    );
                },
                (conId, err) => {
                    Logger.Error("disque failed. state = " + disquuun.connectionState + ", message = " + err.Message);

                    if (disquuun.connectionState != Disquuun.ConnectionState.OPENING &&
                        disquuun.connectionState != Disquuun.ConnectionState.ALLCLOSING)
                    {
                        disquuun.Disconnect();
                        Logger.Error("disque self disconnected.");
                    }

                    disqueReconnector.Reconnect(this, Connect);
                }
            );
        }

        public void Disconnect()
        {
            if (disquuun != null) disquuun.Disconnect();
        }

        public Disquuun GetDisquuun()
        {
            return disquuun;
        }

        public void SetContext(ServerContext context)
        {
            this.context = context;
            context.SetPublisher(Publish);
        }

        public void Publish(ConcurrentQueue<SendQueueRow> queue)
        {
            if (queue.Count == 0)
                return;

            while (queue.Count > 0)
            {
                SendQueueRow row;
                queue.TryDequeue(out row);
                disquuun.Pipeline(disquuun.AddJob(row.ConnectionId, row.Data, 0, "TTL", 60 * 10, "RETRY", 0));
            }

            disquuun.Pipeline().Execute((command, data) => { });
        }



        // こっからフィルタ。
        /*
            フィルタは、staticでいいんで、どっかにコピーして成立させよう。
            
            突きあわせレイヤーだ。
            ConnectionServerと、ServerContextと、DisqueConnectionControllerの三つ巴ポイント。
            
            疎結合にしておけると良い感じなので、このレイヤで何かすべき、っていうレイヤは上位に持っていくと良い気がする。
            ・ServerContext              ゲーム本体、最小単位はDisqueのキューIdになる。データの受け入れと出力をする。
            ・DisqueConnectionController Disqueの管理、送信と受信のハンドラを持つ。
            ・ServerController           
        */

        // webSocket server state for each connection. syncronized to nginx-lua client.lua code.
        public const char STATE_CONNECT = '1';
        public const char STATE_MAINTENANCE_COMMAND = '2';
        public const char STATE_BINARY_MESSAGE = '3';
        public const char STATE_DISCONNECT_INTENT = '4';
        public const char STATE_DISCONNECT_ACCIDT = '5';
        public const char STATE_DISCONNECT_DISQUE_ACKFAILED = '6';
        public const char STATE_DISCONNECT_DISQUE_ACCIDT_SENDFAILED = '7';


        public const int CONNECTION_ID_LEN = 36;// 65D76DEE-0E68-424E-A18F-6D2CC9656FB3

        /**
            受け取ったjobを解釈、contextへと入力する。APIレイヤーはserverContext側にあるので、データを扱うこの辺は変更なしで行けるはず。
        */
        public void InputDatasToContext(List<byte[]> datas)
        {
            // XrossPeer.Log("datas received. datas:" + datas.Count);
            /*
                messageとして受け取ったjobを、list化して読み込む。
            */
            foreach (var dataArray in datas)
            {
                var len = dataArray.Length;
                if (len < 1/*state param*/ + CONNECTION_ID_LEN/*connectionId*/)
                {
                    var invalidMessage = Encoding.ASCII.GetString(dataArray);
                    Logger.Error("illigal format invalidMessage1:" + invalidMessage);
                    continue;
                }

                var state = (char)dataArray[0];

                // dataArray[2-38] is connectionId, length = definitely CONNECTION_ID_LEN.
                var connectionId = Encoding.ASCII.GetString(dataArray, 1, CONNECTION_ID_LEN);

                switch (state)
                {
                    case STATE_CONNECT:
                        {
                            //XrossPeer.Log("STATE_CONNECT");
                            if (1 + CONNECTION_ID_LEN == len)
                            {
                                context.OnConnected(connectionId);
                            }
                            break;
                        }

                    case STATE_MAINTENANCE_COMMAND:
                        {
                            if (1 + CONNECTION_ID_LEN < len)
                            {
                                if ( connectionId != Config.Server.MAINTENANCE_USER )
                                {
                                    Logger.Error("Not maintenance user : "+ connectionId);
                                    continue;
                                }
                                var dataLen = len - (1 + CONNECTION_ID_LEN);
                                var data = new byte[dataLen];
                                Buffer.BlockCopy(dataArray, (1 + CONNECTION_ID_LEN), data, 0, dataLen);
                                context.MaintenanceCommand(data);
                            }
                            break;
                        }

                    case STATE_BINARY_MESSAGE:
                        {
                            if (1 + CONNECTION_ID_LEN < len)
                            {
                                var dataLen = len - (1 + CONNECTION_ID_LEN);
                                var data = new byte[dataLen];
                                Buffer.BlockCopy(dataArray, (1 + CONNECTION_ID_LEN), data, 0, dataLen);
                                context.OnMessage(connectionId, data);
                            }
                            break;
                        }

                    case STATE_DISCONNECT_INTENT:
                        {
                            //XrossPeer.Log("client closed");
                            context.OnDisconnected(connectionId, new byte[0], "intentional disconnect.");
                            break;
                        }

                    case STATE_DISCONNECT_ACCIDT:
                        {
                            Logger.Info("STATE_DISCONNECT_ACCIDT");
                            context.OnDisconnected(connectionId, new byte[0], "accidential disconnect.");
                            break;
                        }
                    case STATE_DISCONNECT_DISQUE_ACKFAILED:
                        {
                            Logger.Info("STATE_DISCONNECT_DISQUE_ACKFAILED");
                            context.OnDisconnected(connectionId, new byte[0], "accidential disconnect.");
                            break;
                        }
                    case STATE_DISCONNECT_DISQUE_ACCIDT_SENDFAILED:
                        {
                            Logger.Info("STATE_DISCONNECT_DISQUE_ACCIDT_SENDFAILED");
                            context.OnDisconnected(connectionId, new byte[0], "send failed to client. disconnect.");
                            break;
                        }

                    default:
                        {
                            Logger.Error("undefined websocket state:" + state);
                            break;
                        }
                }
            }
        }
    }
}
