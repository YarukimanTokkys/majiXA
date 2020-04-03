﻿using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Diagnostics;
using majiXA;
using System.Net;
using System.Collections.Concurrent;

namespace majiXA
{
    public class ServerContext
    {
        // フレーム数
        public double Frame { get; private set; }

        // 1Frameのミリ秒
        const double MsPerFrame = 20;

        // Disqueへデータを送信するQueue
        ConcurrentQueue<SendQueueRow> sendQueue = new ConcurrentQueue<SendQueueRow>();

        // 5分間なんの操作もなければタイムアウト扱い
        TimeSpan timeout = TimeSpan.FromSeconds(60 * 5);

        // フレームレートチェック（オーバーした時にログ出力用）
        Stopwatch stopwatch = new Stopwatch(); 

        // 接続ユーザーの管理 ( string = ConnectionId )
        public ConcurrentDictionary<string, ConnectionInfo> cidConnectionMap = new ConcurrentDictionary<string, ConnectionInfo>();

        // ルーム管理 (int = RoomId)
        public ConcurrentDictionary<int, IRoom> roomIdRoomMap = new ConcurrentDictionary<int, IRoom>();

        // OnMessageで呼ばれるReceiverを管理
        public Dictionary<byte, IProcessing> processingActionDict { get; private set; }

        /// =============================================================================================
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public ServerContext()
        {
            Frame = 0;

            // Receiverを生成
            processingActionDict = new Dictionary<byte, IProcessing>();
            foreach ( eCommand cmd in Enum.GetValues(typeof(eCommand)) )
            {
                string processingName = Config.Common.ProcessingNameSpace + cmd.ToString();
                Type t = Type.GetType(processingName);

                if ( t != null )
                {
                    Logger.Info("Add Processing : "+ processingName);
                    processingActionDict.Add((byte)cmd, (IProcessing)System.Activator.CreateInstance(t));
                }
            }

            Type setup = Type.GetType(Config.Common.SetupClassName);

            if ( setup != null )
            {
                ((ISetup)System.Activator.CreateInstance(setup)).Init();
            }
       }

        /// =============================================================================================
        /// <summary>
        /// クライアントが接続してきた時に呼ばれる
        /// </summary>
        /// <param name="connectionId">接続ID</param>
        public void OnConnected(string connectionId)
        {
            try
            {
                if (cidConnectionMap.ContainsKey(connectionId))
                {
                    Logger.Warning("Already connected! : " + connectionId);
                    return;
                }
                
                Logger.Info("Connected:"+ connectionId);
                cidConnectionMap.TryAdd(connectionId, new ConnectionInfo(connectionId, DateTime.UtcNow + timeout));
            }
            catch (Exception e)
            {
                Logger.Error("OnConncected Error : " + e.Message + e.StackTrace);
            }
        }

        /// =============================================================================================
        /// <summary>
        /// 毎フレーム呼ばれる
        /// </summary>
        public void OnUpdate()
        {
            stopwatch.Reset();
            stopwatch.Start();

            foreach (var room in roomIdRoomMap.Values)
            {
                try
                {
                    room.OnUpdate();
                }
                catch (Exception e)
                {
                    Logger.Error("ServerContext OnUpdate Room Error !! info : " + e.Message + e.StackTrace);
                }
            }

            PublishTo(sendQueue);

            // フレーム遅延した時はログ出力
            if (stopwatch.Elapsed.TotalMilliseconds >= MsPerFrame)
            {
                Logger.Warning(stopwatch.Elapsed.TotalMilliseconds.ToString());
            }

            // タイムアウトチェック　1秒に1回
            if ( Frame%(1000/MsPerFrame) == 0 )
            {
                CheckTimeout();
            }

            Frame++;
        }

        /// =============================================================================================
        /// <summary>
        /// クライアントからデータが届いた時に呼ばれる
        /// </summary>
        /// <param name="connectionId">接続ID</param>
        /// <param name="data">送られてきたデータ</param>
        public void OnMessage(string connectionId, byte[] data)
        {
            Logger.Debug("OnMessage");
            try
            {
                if (data == null || data.Length == 0)
                {
                    Logger.Warning("OnMessage - data is null or empty.");
                    return;
                }
                
                ConnectionInfo cInfo;
                if ( cidConnectionMap.TryGetValue(connectionId, out cInfo) )
                {
                    cInfo.Timeout = DateTime.UtcNow + timeout;

                    byte[] _data = new byte[data.Length - 1];
                    Buffer.BlockCopy(data, 1, _data, 0, _data.Length);

                    byte cmd = data[0];
                    if ( !processingActionDict.ContainsKey(cmd) )
                    {
                        Logger.Error("Unknown command. cmd = " + cmd);
                        return;
                    }

                    Logger.Debug("[OnMessage] Command=" + ((eCommand)cmd).ToString() + " : data.len=" + _data.Length);
                    processingActionDict[cmd].OnMessage(this, cInfo, _data);
                }
                else
                {
                    Logger.Warning("OnMessage - ghost send command. connectionId:" + connectionId);
                }
            }
            catch (Exception e)
            {
                Logger.Error("OnMessage Error : " + e.Message + e.StackTrace);
            }
        }

        /// =============================================================================================
        /// <summary>
        /// クライアントが切断した時に呼ばれる
        /// </summary>
        /// <param name="connectionId">接続ID</param>
        /// <param name="data">送られてきたデータ（実質未使用）</param>
        /// <param name="reason">切断された理由</param>
        public void OnDisconnected(string connectionId, byte[] data, string reason)
        {
            try
            {
                ConnectionInfo cInfo;
                if ( cidConnectionMap.TryRemove(connectionId, out cInfo) )
                {
                    Logger.Debug("Disconnected : " + cInfo.ConnectionId + " : reason = " + reason);

                    IRoom room = GetRoom<IRoom>(cInfo.ConnectionId, cInfo.RoomId);
                    if ( cInfo.RoomId == 0 || room==null )
                    {
                        // 既にルームから抜けているか、ルームが見つからない
                        Logger.Info("No room");
                        return;
                    }

                    if ( room.Leave(cInfo) == 0 )
                    {
                        // roomから全員切断したらルーム削除
                        Logger.Info("Room Delete complete");
                        roomIdRoomMap.TryRemove(room.RoomId, out room);
                    }
                    else
                    {
                        // roomのメンバーが切断したことを他のメンバーに通知
                        Logger.Info("Send Disconnected to roomId="+ room.RoomId);
                        var ret = new List<byte>();
                        ret.Add(Define.CmdDisconnected);
                        ret.AddRange(room.RoomId.ToBytes());
                        ret.AddRange(room.MembersCid.Count.ToBytes());
                        ret.AddRange(cInfo.PlayerNo.ToBytes());
                        Send(ret.ToArray(), room.MembersCid);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error("OnDisconnected Error : " + e.Message + e.StackTrace);
            }
        }

        /// =============================================================================================
        /// <summary>
        /// クライアントにデータを送る
        /// </summary>
        /// <param name="messageData">送信するデータ</param>
        /// <param name="connectionId">送信相手の接続ID</param>
        public void Send(byte[] messageData, string connectionId)
        {
            Send(messageData, new string[] { connectionId });
        }

        /// =============================================================================================
        /// <summary>
        /// クライアントにデータを送る
        /// </summary>
        /// <param name="messageData">送信するデータ</param>
        /// <param name="connectionIds">送信相手の接続ID（複数可）</param>
        public void Send(byte[] messageData, IEnumerable<string> connectionIds)
        {
            foreach (string cid in connectionIds)
            {
                //XrossPeer.Log("Send to [" + cid + "] (" + messageData.Length + "bytes)");
                sendQueue.Enqueue(new SendQueueRow { ConnectionId = cid, Data = messageData });
            }
        }

        /// =============================================================================================
        /// <summary>
        /// メンテナンス用コマンド処理
        /// </summary>
        /// <param name="cmd">メンテナンス用に用意する任意のデータ</param>
        public void MaintenanceCommand(byte[] cmd)
        {
            Type maintenance = Type.GetType(Config.Common.MaintenanceClassName);
            if ( maintenance != null )
            {
                ((IMaintenance)System.Activator.CreateInstance(maintenance)).OnCommand(this, cmd);
            }
        }

        /// =============================================================================================
        /// <summary>
        /// クライアントにエラーを送る
        /// </summary>
        /// <param name="connectionId">送信相手の接続ID</param>
        /// <param name="errMsg">送信するメッセージ</param>
        public void SendError(string connectionId, string errMsg )
        {
            List<byte> ret = new List<byte>();
            ret.Add(Define.CmdError);
            ret.AddRange(System.Text.Encoding.ASCII.GetBytes(errMsg));
            Send(ret.ToArray(), connectionId);
        }

        /// =============================================================================================
        /// <summary>
        /// クライアントに強制切断要求
        /// </summary>
        /// <param name="connectionId">切断してほしいクライアントの接続ID</param>
        public void SendForceClose(string connectionId)
        {
            Send(new byte[]{Define.CmdForceClose}, connectionId);
        }

        /// =============================================================================================
        /// <summary>
        /// PlayerIdから接続ユーザー情報を取得
        /// </summary>
        /// <param name="connectionId">接続ID</param>
        /// <param name="playerId">ユニークID</param>
        /// <param name="cInfo">接続ユーザー情報</param>
        /// <param name="isSendErr">エラー発生時、ユーザーにメッセージを送るかどうか</param>
        /// <returns></returns>
        public bool GetConnectionWithPlayerId(string connectionId, string playerId, out ConnectionInfo cInfo, bool isSendErr = false)
        {
            cInfo = cidConnectionMap.FirstOrDefault(_=>_.Value.PlayerId==playerId).Value;

            if ( cInfo == null || cInfo.PlayerId != playerId )
            {
                return false;
            }

            return true;
        }

        /// =============================================================================================
        /// <summary>
        /// 接続IDから、そのプレイヤーが所属するRoomを取得
        /// </summary>
        /// <param name="connectionId">接続ID</param>
        /// <param name="roomId">ルームID</param>
        /// <param name="isSendErr">エラー発生時、ユーザーにメッセージを送るかどうか</param>
        /// <returns>Roomが取得出来た時は取得したRoom, 取得出来ない場合はnullを返す</returns>
        public T GetRoom<T>(string connectionId, int roomId, bool isSendErr = false) where T : class, IRoom
        {
            if (!roomIdRoomMap.TryGetValue(roomId, out IRoom iroom))
            {
                var errMsg = "Not found room. roomId = " + roomId;
                Logger.Error(errMsg);

                if ( isSendErr )
                {
                    SendError(connectionId, errMsg);
                }
                return null;
            }

            return (T)iroom;
        }

        /// =============================================================================================
        /// <summary>
        /// タイムアウトのチェック
        /// </summary>
        public void CheckTimeout()
        {
            DateTime now = DateTime.UtcNow;
            foreach ( ConnectionInfo con in cidConnectionMap.Values )
            {
                if ( con.Timeout < now )
                {
                    OnDisconnected(con.ConnectionId, new byte[0], "Timeout");
                    SendForceClose(con.ConnectionId);
                }
            }
        }

        /// =============================================================================================
        /// =============================================================================================
        // Disqueにデータを送信する処理と登録する
        Action<ConcurrentQueue<SendQueueRow>> PublishTo = NotYetReady;

        /// <summary>
        /// DisqueConnectionControllerから呼ばれる
        /// </summary>
        /// <param name="publisher">DisqueConnectionControllerのデータ送信処理</param>
        public void SetPublisher(Action<ConcurrentQueue<SendQueueRow>> publisher)
        {
            PublishTo = publisher;
        }

        /// <summary>
        /// PublishToに処理が登録されるまでのダミー処理
        /// </summary>
        /// <param name="data">ダミー</param>
        static void NotYetReady(ConcurrentQueue<SendQueueRow> data)
        {
            //UnityEngine.Debug.Log("not yet publishable.");
        }
    }
}
