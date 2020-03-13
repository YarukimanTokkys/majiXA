using System;
using System.Threading;
using System.Diagnostics;
using majiXA;

namespace ConsoleApplication
{
    public class Program
    {
        static ServerContext serverContext;
        static DisqueConnectionController disqueConnectionCont;

        public static void Main(string[] args)
        {
            ServerManager.Init();
            ServerManager.StartServer();
            GameLoop();
            ServerManager.StopServer();
        }

        public static void GameLoop()
        {
            var stopwatch = new Stopwatch();
            while (true)
            {
                stopwatch.Reset();
                stopwatch.Start();

                ServerManager.OnUpdate();

                var realTime = (1000f / Define.FrameRate) - (stopwatch.Elapsed.TotalMilliseconds);
                var sleepTime = Convert.ToInt32(Math.Floor(realTime));
                if (sleepTime <= 0f)
                    continue;

                Thread.Sleep(sleepTime);
            }
        }
    }
}
