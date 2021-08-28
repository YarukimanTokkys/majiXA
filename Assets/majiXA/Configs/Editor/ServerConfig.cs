using Newtonsoft.Json;
using System.IO;

namespace majiXA
{
    public class ServerConfig
    {
        public static ConfigParam Config;

        public static void Load(string path)
        {
            if (!File.Exists(path))
            {
                Logger.Error("NotFound config.json");
                return;
            }
            var configJson = File.ReadAllText(path);

            Config = JsonConvert.DeserializeObject<ConfigParam>(configJson);

            Logger.Debug("ServerConfig.MAINTENANCE_USER = " + Config.MAINTENANCE_USER);
            Logger.Debug("ServerConfig.LOG_PATH = " + Config.LOG_PATH);
            Logger.Debug("ServerConfig.DISQUE_HOST_IP = " + Config.DISQUE_HOST_IP);
            Logger.Debug("ServerConfig.DISQUE_PORT = " + Config.DISQUE_PORT);
        }
    }

    public class ConfigParam
    {
        public string MAINTENANCE_USER;
        public string LOG_PATH;
        public string DISQUE_HOST_IP;
        public int DISQUE_PORT;
    }
}