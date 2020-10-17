﻿﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WebuSocketCore;

namespace majiXA
{
    public class WebuSocketController : MonoBehaviour
    {
        // クライアントの接続状態を表す
        public enum eConnectStatus : int {
            Disconnected,
            Error,
            Connected,
            Connecting
        }

        // クライアントの接続状態
        public eConnectStatus ConnectStatus { get; private set; }

        // ソケット接続
        public WebuSocket WebuSocket { get; private set; }

        // クライアント <-> サーバ間の通信速度
        public int Rtt { get; private set; }

        // サーバから受け取ったデータを処理するReceiverを保持
        public Dictionary<byte,Action<byte[]>> receiveActionDict { get; private set; }
        

        // ================== ローカル変数 ==================== //
        // クライアント -> サーバにデータ送信処理用のロック
        object sendLock = new object();
        Queue<byte[]> sendQueue = new Queue<byte[]>();

        // クライアント <- サーバのデータ受取処理用のロック
        object lockObj = new object();
        Queue<byte[]> binaryQueue = new Queue<byte[]>();

        // サスペンド中かどうか
        bool IsSuspended = false;
        // Rtt計測の頻度（秒)
        float timeCount = 1f;
        // 接続状態の変更検知用
        eConnectStatus _ConnectStatus = eConnectStatus.Disconnected;
        // 切断・エラーのコード引渡し用
        int reasonCode = 0;
        // Rttの変化検知用
        int _rtt;

        // =================== 各種Action ================== //
        // サーバエラー取得時の処理
        public Action<string> errorAct;
        // 他の誰かが切断した時の処理
        public Action<int,int,int> disconnectedAct;
        // サーバから強制的に切断された時の処理
        public Action<string> forceCloseAct;
        
        // クライアント -> サーバ -> クライアント の通信速度（ミリ秒）
        public Action<int> rttAct;
        // サスペンドした時のイベント
        public Action suspendAct;
        // レジュームした時のイベント（引数はレジューム時の通信状態）
        public Action<eConnectStatus> resumeAct;
        // WebSocket接続完了時に呼ばれる
        public Action connectedAct;
        // WebSocket切断時に呼ばれる（パラメータ = WebuSocketCloseEnum）
        public Action<int> closedAct;
        // WebSocketで接続エラーが発生した時に呼ばれる（パラメータ = WebuSocketErrorEnum
        public Action<int> errorCloseAct;



        void Awake()
        {
            errorAct += ErrorAct;
            disconnectedAct += DisconnectedAct;
            forceCloseAct += ForceCloseAct;

            ConnectStatus = eConnectStatus.Disconnected;
            receiveActionDict = new Dictionary<byte,Action<byte[]>>();
            receiveActionDict.Add(Define.CmdError, ReceiveError);
            receiveActionDict.Add(Define.CmdDisconnected, ReceiveDisconnected);
            receiveActionDict.Add(Define.CmdForceClose, ReceiveForceClosed);
        }
        /// =============================================================================================
        /// <summary>
        /// サーバ側でエラーが発生して、エラーが送信されてきた時に呼ばれる
        /// </summary>
        /// <param name="data">エラーの原因（文字列）</param>
        void ReceiveError(byte[] data)
        {
            int cursor = 0;
            string errorReason = data.ToString(ref cursor);
            errorAct(errorReason);
        }
        /// =============================================================================================
        /// <summary>
        /// 同じルームにいる他の誰かが切断した際に呼ばれる
        /// </summary>
        /// <param name="data">切断情報（ルームID,ルームに残った人数、切断したプレイヤーのplayerNo）</param>
        void ReceiveDisconnected(byte[] data)
        {
            int cursor = 0;
            int roomId = data.ToInt(ref cursor);
            int remainMemberNum = data.ToInt(ref cursor);
            int playerNo = data.ToInt(ref cursor);
            disconnectedAct(roomId, remainMemberNum, playerNo);
        }
        /// =============================================================================================
        /// <summary>
        /// サーバから強制的に切断された際に呼ばれる
        /// </summary>
        /// <param name="data">切断情報（ルームID,ルームに残った人数、切断したプレイヤーのplayerNo）</param>
        void ReceiveForceClosed(byte[] data)
        {
            int cursor = 0;
            string message = ( data.Length>0 ) ? data.ToString(ref cursor) : "";
            forceCloseAct(message);
            Close();
        }

        /// =============================================================================================
        /// <summary>
        /// デフォルトのエラー処理
        /// </summary>
        /// <param name="reason">エラーの原因</param>
        void ErrorAct(string reason)
        {
            Debug.Log("majiXA Error!! : "+ reason);
        }
        
        /// =============================================================================================
        /// <summary>
        /// デフォルトの他の人の切断処理
        /// </summary>
        /// <param name="roomId">切断した人が所属していたルームID</param>
        /// <param name="remainMemberNum">切断した人が所属していたルームに接続している残り人数</param>
        /// <param name="playerNo">切断した人のplayerNo</param>
        void DisconnectedAct(int roomId, int remainMemberNum, int playerNo)
        {
            Debug.Log("Someone disconnected!!\nroomId:"+ roomId +"\nremainMemberNum:"+ remainMemberNum +"\nplayerNo:"+ playerNo);
        }

        /// =============================================================================================
        /// <summary>
        /// デフォルトのサーバからの強制切断処理
        /// </summary>
        /// <param name="message">サーバから送られてきた文言</param>
        void ForceCloseAct(string message)
        {
            Debug.Log("Forced close command from server!!\nmessage:"+ message);
        }
        
        /// =============================================================================================
        /// <summary>
        /// WebSocket接続
        /// </summary>
        /// <param name="serverURL">Ex. ws(wss)://111.222.333.444:8080/disque_clientGameKey</param>
        /// <param name="token">認証機能を使い場合に使用。</param>
        /// <param name="param">UDPを使用する際にポート番号を渡す。</param>
        public void Connect(string ipPort, string gameKey, bool isWss = false, string token = "", string param = "-1")
        {
            if (ConnectStatus != eConnectStatus.Disconnected)
            {
                Debug.LogError("Already connecting or connected");
                return;
            }

            ConnectStatus = eConnectStatus.Connecting;

            var serverURL = string.Format("{0}://{1}/disque_client{2}", (isWss ? "wss" : "ws"), ipPort, gameKey);
            Debug.Log("[majiXA] connecting to : " + serverURL);

            WebuSocket = new WebuSocket(
                serverURL,
                1024 * 100,
                () =>
                {
                    ConnectStatus = eConnectStatus.Connected;
                },
                (Queue<ArraySegment<byte>> datas) =>
                {
                    while (0 < datas.Count)
                    {
                        var data = datas.Dequeue();
                        var bytes = new byte[data.Count];
                        Buffer.BlockCopy(data.Array, data.Offset, bytes, 0, data.Count);
                        lock (lockObj)
                        {
                            binaryQueue.Enqueue(bytes);
                        }
                    }
                },
                () =>
                {
                    // pinged.
                },
                (WebuSocketCloseEnum closedReason) =>
                {
                    ConnectStatus = eConnectStatus.Disconnected;
                    reasonCode = (int)closedReason;
                },
                (WebuSocketErrorEnum errorMessage, Exception e) =>
                {
                    ConnectStatus = eConnectStatus.Error;
                    reasonCode = (int)errorMessage;
                },
                new Dictionary<string, string>{
                    {"param", param},
                    {"token", token}
                }
            );
        }

        public void ConnectStatusHandler()
        {
            if ( _ConnectStatus == ConnectStatus)
            {
                return;
            }

            switch ( ConnectStatus )
            {
                case eConnectStatus.Connected:
                    Debug.Log("[majiXA] connected");
                    connectedAct?.Invoke();
                    break;
                case eConnectStatus.Disconnected:
                    Debug.Log("[majiXA] closed : "+ (WebuSocketCloseEnum)reasonCode);
                    closedAct?.Invoke(reasonCode);
                    break;
                case eConnectStatus.Error:
                    Debug.LogError("[majiXA] error : "+ (WebuSocketErrorEnum)reasonCode);
                    errorCloseAct?.Invoke(reasonCode);
                    ConnectStatus = eConnectStatus.Disconnected;
                    break;
            }

            _ConnectStatus = ConnectStatus;
        }

        /// =============================================================================================
        /// <summary>
        /// データ送受信用ループ
        /// </summary>
        public void FixedUpdate()
        {
            ConnectStatusHandler();

            if ( ConnectStatus != eConnectStatus.Connected )
            {
                return;
            }

            try
            {
                // Queueに貯まった送信データを送信
                if (sendQueue.Count > 0)
                {
                    lock (sendLock)
                    {
                        foreach (var data in sendQueue)
                        {
                            WebuSocket.Send(data);
                        }
                        sendQueue.Clear();
                    }
                }

                // サーバから送信されてきたデータを取得
                if (0 < binaryQueue.Count)
                {
                    lock (lockObj)
                    {
                        foreach (var data in binaryQueue)
                        {
                            Receive(data);
                        }
                        binaryQueue.Clear();
                    }
                }

                // RTTチェック（数値に変化があった時だけAction着火）
                timeCount -= Time.deltaTime;
                if ( timeCount<0f)
                {
                    WebuSocket.Ping((rtt) => { Rtt = rtt;});
                    timeCount = 1f;
                }
                if ( _rtt != Rtt )
                {
                    rttAct?.Invoke(Rtt);
                    _rtt = Rtt;
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToString());
            }
        }

        /// =============================================================================================
        /// <summary>
        /// WebSocket切断
        /// </summary>
        public void Close()
        {
            if (WebuSocket == null)
            {
                ConnectStatus = eConnectStatus.Disconnected;
                return;
            }
            WebuSocket.Disconnect();
            WebuSocket = null;
        }

        /// =============================================================================================
        /// <summary>
        /// サーバへデータ送信
        /// </summary>
        /// <param name="data">送信したいバイナリデータ</param>
        public void Send(byte[] data)
        {
            if (ConnectStatus == eConnectStatus.Disconnected)
            {
                Debug.LogError("Not connected yet. Try after connected.");
                return;
            }

            lock (sendLock)
            {
                sendQueue.Enqueue(data);
            }
        }

        /// =============================================================================================
        /// <summary>
        /// サーバからのデータを受信
        /// </summary>
        /// <param name="data">受け取ったデータ</param>
        public void Receive(byte[] data)
        {
            byte cmd = data[0];

            byte[] d = new byte[data.Length - 1];
            if (data.Length > 1)
            {
                Array.Copy(data, 1, d, 0, d.Length);
            }
//            Debug.Log("[Receive] command=" + cmd.ToString() + " : data.len=" + data.Length);

            if( receiveActionDict.ContainsKey(cmd) )
            {
                receiveActionDict[cmd](d);
            }
            else
            {
                Debug.LogError("Unknown command : command="+ cmd);
            }
        }
        
        /// =============================================================================================
        /// <summary>
        /// 受信処理をDictionaryに登録
        /// </summary>
        /// <param name="cmd">対応するコマンドをbyteにしたもの（Commands.csに記載）</param>
        /// <param name="act">対応するコマンドが送られてきた時に実装する処理</param>
        public void SetReceiveAction(byte cmd, System.Action<byte[]> act )
        {
            receiveActionDict[cmd] = act;
        }

        /// =============================================================================================
        /// <summary>
        /// DestroyのタイミングでWebSocket切断
        /// </summary>
        void OnDestroy()
        {
            Close();
        }

        void OnApplicationPause(bool pause)
        {
            if ( pause && !IsSuspended )
            {
                Debug.Log("[Suspend]");
                suspendAct?.Invoke();
                IsSuspended = true;
            }
            else if ( !pause && IsSuspended )
            {
                Debug.Log("[Resume]");
                resumeAct?.Invoke(ConnectStatus);
                IsSuspended = false;
            }
        }



    }
}