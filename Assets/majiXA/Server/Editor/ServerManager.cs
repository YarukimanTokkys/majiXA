namespace  majiXA
{
    public class ServerManager
    {
        public static ServerContext serverContext;
        static DisqueConnectionController disqueConnectionController;
        public static bool Stat;
        public static bool ServerAutoStart;

        public static void Init()
        {
            Logger.Init(Config.Server.LOG_PATH);
            Logger.Info("[SERVER LOG] SETUP.");
        }

        /// =============================================================================================
        /// <summary>
        /// Unity上でContextサーバを起動します
        /// </summary>
        public static void StartServer()
        {
            serverContext = new ServerContext();
            disqueConnectionController = new DisqueConnectionController("disque_client" + Config.Common.BUNDLE_IDENTIFIER + "_context", Config.Server.DISQUE_HOST_IP, Config.Server.DISQUE_PORT);
            disqueConnectionController.SetContext(serverContext);

            Logger.Info("[SERVER START] GameKey : " + Config.Common.BUNDLE_IDENTIFIER);

            Stat = true;
        }

        /// =============================================================================================
        /// <summary>
        /// Unity上で動作しているContextサーバを停止します
        /// </summary>
        public static void StopServer()
        {
            serverContext = null;

            if (disqueConnectionController != null)
            {
                disqueConnectionController.Disconnect();
                disqueConnectionController = null;
            }

            Logger.Info("[SERVER STOP]");

            Stat = false;
        }

        /// =============================================================================================
        /// <summary>
        /// ゲームループ
        /// </summary>
        public static void OnUpdate()
        {
            if (serverContext != null)
            {
                serverContext.OnUpdate();
                Logger.SetFrame(serverContext.Frame);
            }
        }
    }
}