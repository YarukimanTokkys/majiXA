using System;
using System.Threading;
using DisquuunCore;

namespace majiXA
{
    public class DisqueReconnector
    {
        private readonly string contextQueueIdentity;
        private readonly string ip;
        private readonly int port;

        private int retryInterval;

        private int retryCount = 0;
        private bool isExecute = false;

        public DisqueReconnector(string contextQueueIdentity, string ip, int port)
        {
            this.contextQueueIdentity = contextQueueIdentity;
            this.ip = ip;
            this.port = port;
        }

        public void Reconnect(DisqueConnectionController dcc, Action<string, string, int> action)
        {
            if (isExecute)
                return;

            Logger.Info("DisqueReconnector Execute();");

            if (action == null)
                throw new ArgumentNullException("action");

            retryCount = 0;
            isExecute = true;

            while (true)
            {
                retryInterval = (5 + (5 * retryCount)) * 1000;
                if (retryInterval > (60 * 1000))
                    retryInterval = (60 * 1000);

                Logger.Info("DisqueReconnector action(); RetryCount = " + retryCount + ", Sleep ms = " + retryInterval);
                action(contextQueueIdentity, ip, port);
                Thread.Sleep(retryInterval);

                var disquuun = dcc.GetDisquuun();
                if (disquuun != null)
                {
                    Logger.Info("DisqueReconnector disquuun.connectionState = " + disquuun.connectionState);

                    if (disquuun.connectionState == Disquuun.ConnectionState.OPENED)
                    {
                        Logger.Info("DisqueReconnector reconnect completed.");
                        break;
                    }
                }

                retryCount++;
            }

            isExecute = false;
        }
    }
}
