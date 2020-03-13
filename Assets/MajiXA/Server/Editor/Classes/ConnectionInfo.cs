namespace majiXA
{
    /// <summary>
    /// 接続しているプレイヤーの情報を管理
    /// </summary>
    public class ConnectionInfo
    {
        public string ConnectionId;
        public string PlayerId;
        public System.DateTime Timeout;
        
        public int PlayerNo;
        public int RoomId;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="cid">接続ID</param>
        /// <param name="timeout">タイムアウト判定にする時間</param>
        public ConnectionInfo(string cid, System.DateTime timeout)
        {
            ConnectionId = cid;
            Timeout = timeout;
            PlayerNo = -1;
        }
    }
}