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
            Connected,
            Connecting
        }

        // クライアントの接続状態
        public eConnectStatus ConnectStatus { get; private set; }

        // ソケット接続
        public WebuSocket WebuSocket { get; private set; }

        // クライアント -> サーバにデータ送信処理用のロック
        object sendLock = new object();
        Queue<byte[]> sendQueue = new Queue<byte[]>();

        // クライアント <- サーバのデータ受取処理用のロック
        object lockObj = new object();
        Queue<byte[]> binaryQueue = new Queue<byte[]>();

        // クライアント <-> サーバ間の通信速度
        public int Rtt { get; private set; }

        // Rtt計測の頻度（秒 )
        float timeCount = 1f;

        // サーバから受け取ったデータを処理するReceiverを保持
        public Dictionary<byte,Action<byte[]>> receiveActionDict { get; private set; }
        
        // サーバエラー取得時の処理
        public Action<string> errorAct;
        // 他の誰かが切断した時の処理
        public Action<int,int,int> disconnectedAct;
        // サーバから強制的に切断された時の処理
        public Action<string> forceCloseAct;

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
            Debug.Log("MajiXA Error!! : "+ reason);
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

            WebuSocket = new WebuSocket(
                serverURL,
                1024 * 100,
                () =>
                {
                    Debug.Log("connected to server:" + serverURL);
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
                    Debug.Log("get Ping");
                },
                (WebuSocketCloseEnum closedReason) =>
                {
                    Debug.LogWarning("connection closed by reason:" + closedReason);
                    ConnectStatus = eConnectStatus.Disconnected;
                },
                (WebuSocketErrorEnum errorMessage, Exception e) =>
                {
                    Debug.LogError("connection error:" + errorMessage);
                    ConnectStatus = eConnectStatus.Disconnected;
                },
                new Dictionary<string, string>{
                    {"param", param},
                    {"token", token}
                }
            );
        }

        /// =============================================================================================
        /// <summary>
        /// データ送受信用ループ
        /// </summary>
        public void FixedUpdate()
        {
            if (ConnectStatus != eConnectStatus.Connected)
            {
                return;
            }

            // Queueに貯まった送信データを送信
            if (sendQueue.Count > 0)
            {
                lock (sendLock)
                {
                    foreach (var data in sendQueue)
                    {
                        try
                        {
                            WebuSocket.Send(data);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError(e.ToString());
                        }
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
                        try
                        {
                            Receive(data);
                        }
                        catch (Exception e)
                        {
                            UnityEngine.Debug.LogError(e.ToString());
                        }
                    }
                    binaryQueue.Clear();
                }
            }

            // RTTチェック
            timeCount -= Time.deltaTime;
            if ( timeCount<0f)
            {
                WebuSocket.Ping((rtt) =>
                {
                    Rtt = rtt;
                });
                timeCount = 1f;
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
    }
}