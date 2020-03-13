#if UNITY_EDITOR
using UnityEditor;
using System;
using UnityEngine;

namespace majiXA
{
    /// <summary>
    /// Unity上でServerContextを動かすためのクラス
    /// </summary>
    [InitializeOnLoad]
    public class ServerInitializer : Editor
    {
        static ServerInitializer()
        {
            if ( string.IsNullOrEmpty(Config.Common.BUNDLE_IDENTIFIER) )
            {
                Debug.LogError("BUNDLE_IDENTIFIER を設定してください。 (Config.cs)");
                return;
            }
            else if ( Config.Common.BUNDLE_IDENTIFIER == "com.Company.ProductName" )
            {
                Debug.LogError("BUNDLE_IDENTIFIER はユニークになるように設定してください。 (Config.cs)");
                return;
            }
            ServerManager.Init();
            EditorApplication.playModeStateChanged += OnChangedPlayMode;
        }
        public static void StartServer()
        {
            EditorApplication.update += OnUpdate;
            ServerManager.StartServer();
        }
        public static void StopServer()
        {
            EditorApplication.update -= OnUpdate;
            ServerManager.StopServer();
        }
        public static void OnUpdate()
        {
            ServerManager.OnUpdate();
        }

        /// =============================================================================================
        /// <summary>
        /// プレイモードが変更された
        /// </summary>
        /// <param name="state">変更されたモード</param>
        private static void OnChangedPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                StopServer();
            }
        }
    }
}
#endif
