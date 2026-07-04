using System;
using System.IO;
using ModKit.Helper;
using ModKit.Internal;
using Newtonsoft.Json;

namespace ASN
{
    public static class ASNConfigHandler
    {
        private static string GetConfigPath(string basePluginsPath)
        {
            string directoryPath = Path.Combine(basePluginsPath, AssemblyHelper.GetName());
            
            if (!Directory.Exists(directoryPath)) 
                Directory.CreateDirectory(directoryPath);
            
            return Path.Combine(directoryPath, "config.json");
        }

        public static Config LoadConfig(string basePluginsPath, string pluginVersion)
        {
            string filePath = GetConfigPath(basePluginsPath);
            Config config = new Config();

            try
            {
                if (File.Exists(filePath))
                {
                    string existingJson = File.ReadAllText(filePath);
                    JsonConvert.PopulateObject(existingJson, config);

                    if (config.ConfigVersion != pluginVersion)
                    {
                        // La reecriture ci-dessous ajoute les champs manquants en conservant les valeurs existantes
                        string oldVersion = string.IsNullOrEmpty(config.ConfigVersion) ? "inconnue" : config.ConfigVersion;
                        Logger.LogWarning("ASN - Config", $"Config mise a jour : {oldVersion} -> {pluginVersion} (nouveaux champs ajoutes, valeurs conservees).");
                        config.ConfigVersion = pluginVersion;
                    }
                    else
                    {
                        Logger.LogSuccess("ASN - Config", "Configuration chargee");
                    }
                }
                else
                {
                    config.ConfigVersion = pluginVersion;
                    Logger.LogWarning("ASN - Config", "Fichier config.json cree ! Configurez les webhooks Discord.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("ASN - Config", $"LoadConfig: {ex.Message}");
                config.ConfigVersion = pluginVersion;
            }

            SaveConfig(config, basePluginsPath);
            return config;
        }

        public static void SaveConfig(Config config, string basePluginsPath)
        {
            try
            {
                string filePath = GetConfigPath(basePluginsPath);
                string jsonContent = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(filePath, jsonContent);
            }
            catch (Exception ex)
            {
                Logger.LogError("ASN - Config", $"SaveConfig: {ex.Message}");
            }
        }
    }
}