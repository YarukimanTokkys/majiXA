﻿namespace majiXA.Config
{
    public class Server
    {
        public const string MAINTENANCE_USER = "000-Backdoor_For_Maintenanceuser-000";

        #if UNITY_EDITOR
            // UnityEditorでContextを動かす時の設定
            public static string LOG_PATH = UnityEngine.Application.dataPath + "/../server.log";

            public const string DISQUE_HOST_IP = "203.137.165.35";
            public const int DISQUE_PORT = 7711;
        #else
            // (Linux)サーバでContextを動かす時の設定
            public static string LOG_PATH = "/var/log/server.log";

            public const string DISQUE_HOST_IP = "127.0.0.1";
            public const int DISQUE_PORT = 7711;
        #endif
    }
}