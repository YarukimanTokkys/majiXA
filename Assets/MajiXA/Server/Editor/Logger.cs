using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Globalization;

namespace MajiXA
{
    public class Logger
    {
        static object lockObject = new object();

        static double frame = 0;
        static int logCount = 0;

        static int logPool = 10;
        static string logPath = "test.log";
        static StringBuilder logs = new StringBuilder();

        static void _Log(string message, string logLevel, bool export = false)
        {
            lock (lockObject)
            {
                export = false;
                logs.AppendFormat("[{0}] {1} Frame:{2} {3}", DateTime.Now.ToString("[yyyy/MM/dd HH:mm:ss] "), logLevel, frame, message);

                logCount++;
                if (logCount >= logPool)
                {
                    export = true;
                    logCount = 0;
                }

                if (!export)
                {
                    logs.AppendLine();
                    return;
                }

                // file write
                using (var fs = new FileStream( logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite) )
                {
                    using (var sr = new StreamWriter(fs))
                    {
                        sr.WriteLine(logs.ToString());
                        logs.Clear();
                    }
                }
            }
        }

        // エラー系　リリース後も残しておきたいログ
        public static void Error(string message)
        {
            _Log( message,"== Error == ", true);
        }
        // エラーじゃないけどリリース後も残しておきたいログ
        public static void Warning(string message)
        {
            _Log(message,"== Warning == ", true);
        }

        // デバッグ表示用　サーバ起動時のログ　リリース時は残しておいてもいいかも
        public static void Info(string message)
        {
            _Log(message,"== Info == ", true);
        }
        // デバッグ表示用　プレイ中のコマンドとか。リリース後は消したい。
        public static void Debug(string log)
        {
            _Log(log, "", true);
        }

        public static void Init(string filepath, int pool = 1)
        {
            logPath = filepath;
            logPool = pool;
        }

        public static void SetFrame(double f)
        {
            frame = f;
        }
    }
}