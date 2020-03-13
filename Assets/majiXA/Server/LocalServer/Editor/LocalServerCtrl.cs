using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

namespace majiXA
{
    public class LocalServerCtrl : EditorWindow
    {
        [MenuItem ("majiXA/Local Server Controller")]
        static void Init()
        {
            var window = (LocalServerCtrl)EditorWindow.GetWindow(typeof(LocalServerCtrl));
            window.Show();
        }

        void OnGUI()
        {
            GUILayout.Space(10f);

            if ( ServerManager.Stat )
            {
                Color defColor = GUI.color;
                Color defContentColor = GUI.contentColor;
                GUI.color = Color.cyan;
                GUI.contentColor = Color.white;
                if ( GUILayout.Button("STOP") )
                {
                    if ( ServerManager.Stat == true )
                    {
                        ServerInitializer.StopServer();
                        return;
                    }
                }

                GUI.color = defColor;
                GUI.contentColor = defContentColor;
                ServerInfo(ServerManager.serverContext);
            }
            else
            {
                if ( GUILayout.Button("START") )
                {
                    if ( ServerManager.Stat == false )
                    {
                        ServerInitializer.StartServer();
                    }
                }
            }
        }

        int cnt = 0;
        void Update()
        {
            if ( !ServerManager.Stat )
            {
                return;
            }
            if ( cnt==0)
            {
                Repaint();
                cnt=20;
                return;
            }
            cnt--;
        }

        bool isPlayerInfoOpen = false;
        bool isRoomInfoOpen = false;

        void ServerInfo(ServerContext sc)
        {
            GUILayout.Space(10f);

            isPlayerInfoOpen = EditorGUILayout.Foldout(isPlayerInfoOpen, "接続プレイヤー数 : "+ sc.cidConnectionMap.Count +"人");
            
            if ( isPlayerInfoOpen )
            {
                ShowPlayerInfo(sc.cidConnectionMap.Values);
            }

            isRoomInfoOpen = EditorGUILayout.Foldout(isRoomInfoOpen, "ルーム数 : "+ sc.roomIdRoomMap.Count+" ルーム");
            if ( isRoomInfoOpen )
            {
                ShowRoomInfo(sc.roomIdRoomMap.Values);
            }
            
        }

        Vector2 playerInfoScrollPosition;

        void ShowPlayerInfo(ICollection<ConnectionInfo> cInfos)
        {
            EditorGUI.indentLevel++;

            int height = Math.Min( Math.Max(cInfos.Count*20, 100) , 200);
            
            playerInfoScrollPosition = EditorGUILayout.BeginScrollView(playerInfoScrollPosition,GUILayout.Height(height));{

            EditorGUILayout.BeginHorizontal();{
                EditorGUILayout.TextField("ConnectionId",GUILayout.Width(100));
                EditorGUILayout.TextField("PlayerId",GUILayout.Width(100));
                EditorGUILayout.TextField("PlayerNo",GUILayout.Width(70));
                EditorGUILayout.TextField("RoomNo",GUILayout.Width(70));
                EditorGUILayout.TextField("Timeout",GUILayout.Width(150));
            }EditorGUILayout.EndHorizontal();



            foreach ( var cInfo in cInfos )
            {
                EditorGUILayout.BeginHorizontal();{
                    EditorGUILayout.TextField(cInfo.ConnectionId,GUILayout.Width(100));
                    EditorGUILayout.TextField(cInfo.PlayerId,GUILayout.Width(100));
                    EditorGUILayout.TextField(cInfo.PlayerNo.ToString(),GUILayout.Width(70));
                    EditorGUILayout.TextField(cInfo.RoomId.ToString(),GUILayout.Width(70));
                    EditorGUILayout.TextField(cInfo.Timeout.ToString("yyyy-MM-dd HH:mm:ss"),GUILayout.Width(150));
                }EditorGUILayout.EndHorizontal();
            }
            }EditorGUILayout.EndScrollView();

            EditorGUI.indentLevel--;
        }

        Vector2 roomInfoScrollPosition;

        void ShowRoomInfo(ICollection<IRoom> rooms)
        {
            EditorGUI.indentLevel++;
            
            int height = Math.Min( Math.Max(rooms.Count*20, 100) , 200);

           roomInfoScrollPosition = EditorGUILayout.BeginScrollView(roomInfoScrollPosition,GUILayout.Height(height));{

            EditorGUILayout.BeginHorizontal();{
                EditorGUILayout.TextField("RoomId",GUILayout.Width(100));
                EditorGUILayout.TextField("MemberNum",GUILayout.Width(100));
                EditorGUILayout.TextField("Capacity",GUILayout.Width(100));
            }EditorGUILayout.EndHorizontal();



            foreach ( var room in rooms )
            {
                EditorGUILayout.BeginHorizontal();{
                    EditorGUILayout.TextField(room.RoomId.ToString(),GUILayout.Width(100));
                    EditorGUILayout.TextField(room.MembersCid.Count.ToString(),GUILayout.Width(100));
                    EditorGUILayout.TextField(room.Capacity.ToString().ToString(),GUILayout.Width(100));
                }EditorGUILayout.EndHorizontal();
            }
            }EditorGUILayout.EndScrollView();

            EditorGUI.indentLevel--;
        }
    }
}