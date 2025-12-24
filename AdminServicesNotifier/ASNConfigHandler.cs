using System.IO;
using ModKit.Helper;
using Newtonsoft.Json;

namespace ASN
{
    public static class ASNConfigHandler
    {
        private static string GetConfigPath(string basePluginsPath)
        {
            string directoryPath = Path.Combine(basePluginsPath, AssemblyHelper.GetName());
            if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);
            return Path.Combine(directoryPath, "config.json");
        }

        public static Config LoadConfig(string basePluginsPath)
        {
            string filePath = GetConfigPath(basePluginsPath);
            Config config = new Config();

            if (File.Exists(filePath))
            {
                string existingJson = File.ReadAllText(filePath);
                JsonConvert.PopulateObject(existingJson, config);
            }

            SaveConfig(config, basePluginsPath); 
            return config;
        }

        public static void SaveConfig(Config config, string basePluginsPath)
        {
            string filePath = GetConfigPath(basePluginsPath);
            string jsonContent = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(filePath, jsonContent);
        }
    }
}