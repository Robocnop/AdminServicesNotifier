using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Life;
using Life.DB;
using Life.UI;
using Life.Network;
using Life.Network.Systems;
using Mirror;
using ModKit.Helper;
using ModKit.Interfaces;
using ModKit.Internal;
using ModKit.Helper.DiscordHelper;
using _menu = AAMenu.Menu;

namespace ASN
{
    public class AdminServicesNotifier : ModKit.ModKit
    {
        public Config Config { get; private set; }
        private Dictionary<int, DateTime> _serviceSessions = new Dictionary<int, DateTime>();
        private Dictionary<int, bool> _confirmedStates = new Dictionary<int, bool>();
        private List<int> _isPanelActive = new List<int>();

        public AdminServicesNotifier(IGameAPI api) : base(api)
        {
            PluginInformations = new PluginInformations(AssemblyHelper.GetName(), "2.0.0", "Robocnop");
        }

        public override void OnPluginInit()
        {
            base.OnPluginInit();
            ModKit.Internal.Logger.LogSuccess($"{PluginInformations.SourceName} v{PluginInformations.Version}", "initialisé");
            
            Config = ASNConfigHandler.LoadConfig(pluginsPath);

            InsertMenu();
            InsertInteractionPutAdminOn();

            // --- COMMANDES ---
            new SChatCommand("/a", "Voir les admins en service", "/a", (player, args) => 
            {
                if (Config.AllowPlayerToSeeAdmin || player.IsAdmin)
                {
                    List<string> adminsOnDuty = new List<string>();
                    foreach (Player p in Nova.server.GetAllPlayers())
                        if (p.IsAdminService) adminsOnDuty.Add(p.account.username);

                    string msg = adminsOnDuty.Count > 0 ? $"Admins en service : {string.Join(", ", adminsOnDuty)}" : "Aucun admin en service.";
                    player.SendText($"<color=#1c9d43>[STAFF]</color> {msg}");
                }
                else player.Notify("Erreur", "Permission insuffisante.", NotificationManager.Type.Error);
            }).Register();

            new SChatCommand("/sa", "Prise ou fin de service admin", "/sa", (player, args) => {
                if (player.IsAdmin) ConfirmServiceToggle(player);
            }).Register();

            CheckBypassLoop();
        }

        // watchdog : Vérifie chaque seconde si un admin a forcé son service
        private async void CheckBypassLoop()
        {
            while (true)
            {
                await Task.Delay(1000);
                foreach (Player p in Nova.server.GetAllPlayers())
                {
                    if (p == null || !p.IsAdmin || p.setup?.character == null) continue;
                    int id = p.setup.character.Id;
                    bool confirmed = _confirmedStates.ContainsKey(id) && _confirmedStates[id];
                    
                    if (p.IsAdminService != confirmed && !_isPanelActive.Contains(id))
                    {
                        p.IsAdminService = confirmed;
                        ConfirmServiceToggle(p);
                    }
                }
            }
        }

        // disconnect : Gère la sortie propre des logs (broken pour l'instant need un fix)
        public override void OnPlayerDisconnect(NetworkConnection conn)
        {
            base.OnPlayerDisconnect(conn);
            Player player = Nova.server.GetPlayer(conn);

            if (player?.IsAdmin == true && player.IsAdminService && player.setup?.character != null)
            {
                int id = player.setup.character.Id;
                _confirmedStates[id] = false;
                string duration = StopTrackingAndGetDuration(player);
                _ = SendEmbedLog(Config.AdminUseServiceAdminWebhookUrl, "#e67e22", "🟠 DÉCONNEXION EN SERVICE", player, duration);
            }
        }

        // panel : La fenêtre UI de confirmation (Oui/Non)
        public void ConfirmServiceToggle(Player player)
        {
            if (player.setup?.character == null) return;
            int id = player.setup.character.Id;
            if (_isPanelActive.Contains(id)) return;
            _isPanelActive.Add(id);

            bool isInService = player.IsAdminService;
            Panel panel = PanelHelper.Create(isInService ? "Quitter le service ?" : "Prendre le service ?", UIPanel.PanelType.Tab, player, () => ConfirmServiceToggle(player));
            
            panel.AddButton("Fermer", ui => { _isPanelActive.Remove(id); player.ClosePanel(panel); });
            panel.AddButton("Valider", ui => ui.SelectTab());

            panel.AddTabLine("Non", ui => { 
                _isPanelActive.Remove(id); 
                player.ClosePanel(panel); 
            });

            panel.AddTabLine("Oui", async ui =>
            {
                _isPanelActive.Remove(id);
                player.ClosePanel(panel);
                if (!isInService)
                {
                    _confirmedStates[id] = true;
                    player.IsAdminService = true;
                    StartTracking(player);
                    await SendEmbedLog(Config.AdminUseServiceAdminWebhookUrl, "#2ecc71", "🟢 PRISE DE SERVICE", player);
                    Nova.server.SendMessageToAll($"<color=#ff0202>[Serveur] <color=#ffffff>L'Admin {player.account.username} est disponible</color>");
                }
                else
                {
                    _confirmedStates[id] = false;
                    player.IsAdminService = false;
                    string duration = StopTrackingAndGetDuration(player);
                    await SendEmbedLog(Config.AdminUseServiceAdminWebhookUrl, "#e74c3c", "🔴 FIN DE SERVICE", player, duration);
                    Nova.server.SendMessageToAll($"<color=#ff0202>[Serveur] <color=#ffffff>L'Admin {player.account.username} est indisponible</color>");
                }
            });
            player.ShowPanelUI(panel);
        }

        // embeds : Système d'envoi DiscordHelper
        private async Task SendEmbedLog(string url, string hexColor, string title, Player player, string duration = null)
        {
            if (string.IsNullOrEmpty(url) || url == "URL_ICI") return;
            DiscordWebhookClient client = new DiscordWebhookClient(url);

            string steamId = player.account.steamId.ToString();
            string rpName = (player.setup?.character != null) ? $"{player.setup.character.Firstname} {player.setup.character.Lastname}" : "Inconnu";

            List<string> fieldNames = new List<string> { "🎭 Nom RP", "🆔 ID Perso", "👤 Compte", "🎮 Nom Steam", "💾 SteamID", "🔗 Profil" };
            List<string> fieldValues = new List<string> { rpName, player.setup.character.Id.ToString(), player.account.username, player.steamUsername, steamId, $"[Cliquez ici](https://steamcommunity.com/profiles/{steamId})" };

            if (!string.IsNullOrEmpty(duration))
            {
                fieldNames.Add("⏳ Durée du service");
                fieldValues.Add($"**{duration}**");
            }

            await DiscordHelper.SendEmbed(client, hexColor, title, "Log AdminServicesNotifier v2.0.0", fieldNames, fieldValues, false, true, $"Fait par Robocnop • {DateTime.Now:HH:mm}");
        }

        // tracking : Gestion du temps de service
        private void StartTracking(Player player) { if (player.setup?.character != null) _serviceSessions[player.setup.character.Id] = DateTime.Now; }
        private string StopTrackingAndGetDuration(Player player) {
            if (player.setup?.character != null && _serviceSessions.ContainsKey(player.setup.character.Id)) {
                TimeSpan d = DateTime.Now - _serviceSessions[player.setup.character.Id];
                _serviceSessions.Remove(player.setup.character.Id);
                return $"{d.Hours}h {d.Minutes}m {d.Seconds}s";
            }
            return "Inconnu";
        }

        // menus & interaction
        public void InsertMenu() {
            _menu.AddAdminTabLine(PluginInformations, 1, "AdminServicesNotifier", (ui) => ConfirmServiceToggle(PanelHelper.ReturnPlayerFromPanel(ui)));
            _menu.AddAdminPluginTabLine(PluginInformations, 1, "AdminServicesNotifier", (ui) => ConfirmServiceToggle(PanelHelper.ReturnPlayerFromPanel(ui)), 0);
        }

        public void InsertInteractionPutAdminOn() {
            _menu.AddInteractionTabLine(PluginInformations, "Gestion du service admin", (ui) => {
                Player p = PanelHelper.ReturnPlayerFromPanel(ui);
                if (p.IsAdmin) ConfirmServiceToggle(p);
            });
        }

        // spawn : Affiche le panel de service dès que l'admin arrive
        public override async void OnPlayerSpawnCharacter(Player player, NetworkConnection conn, Characters character) {
            base.OnPlayerSpawnCharacter(player, conn, character);
            if (player?.IsAdmin == true) {
                if (player.setup?.character != null) _confirmedStates[player.setup.character.Id] = false;
                ConfirmServiceToggle(player); // C'est ici que le panel s'ouvre au spawn
                await SendEmbedLog(Config.AdminLoginWebhookUrl, "#3498db", "🔵 CONNEXION ADMIN", player);
            }
        }
    }
}
