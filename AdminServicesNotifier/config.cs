namespace ASN
{
    public class Config
    {
        // Gere par le plugin : permet de detecter une config issue d'une ancienne version
        public string ConfigVersion { get; set; } = "";
        public string AdminLoginWebhookUrl { get; set; } = "URL_ICI";
        public string AdminUseServiceAdminWebhookUrl { get; set; } = "URL_ICI";
        public bool AllowPlayerToSeeAdmin { get; set; } = true;
        public bool OpenPanelOnSpawn { get; set; } = true;
        public bool CheckForUpdates { get; set; } = true;
    }
}