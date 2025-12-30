using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Life;
using Life.DB;
using Life.UI;
using Life.Network;
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
        private Dictionary<int, bool> _silentMode = new Dictionary<int, bool>();
        private List<int> _isPanelActive = new List<int>();
        private bool _isRunning = false;

        public AdminServicesNotifier(IGameAPI api) : base(api)
        {
            PluginInformations = new PluginInformations(AssemblyHelper.GetName(), "2.1.0", "Robocnop");
        }

        public override void OnPluginInit()
        {
            base.OnPluginInit();
            Logger.LogSuccess("ASN - Demarrage", $"v{PluginInformations.Version} initialise");
            
            Config = ASNConfigHandler.LoadConfig(pluginsPath);

            InsertMenu();
            InsertInteractionPutAdminOn();

            new SChatCommand("/a", "Voir les admins en service", "/a", (player, args) => 
            {
                if (Config.AllowPlayerToSeeAdmin || player.IsAdmin)
                {
                    List<string> adminsOnDuty = new List<string>();
                    foreach (Player p in Nova.server.GetAllPlayers())
                    {
                        if (p != null && p.IsAdminService && p.setup?.character != null)
                        {
                            int id = p.setup.character.Id;
                            bool isSilent = _silentMode.ContainsKey(id) && _silentMode[id];
                            
                            if (!isSilent)
                                adminsOnDuty.Add(p.account.username);
                        }
                    }

                    string msg = adminsOnDuty.Count > 0 
                        ? $"Admins en service : {string.Join(", ", adminsOnDuty)}" 
                        : "Aucun admin en service.";
                    
                    player.SendText($"<color=#1c9d43>[STAFF]</color> {msg}");
                }
                else 
                {
                    player.Notify("Erreur", "Permission insuffisante.", NotificationManager.Type.Error);
                }
            }).Register();

            new SChatCommand("/sa", "Prise ou fin de service admin", "/sa", (player, args) => 
            {
                if (player.IsAdmin) 
                    ConfirmServiceToggle(player);
                else
                    player.Notify("Erreur", "Commande reservee aux admins.", NotificationManager.Type.Error);
            }).Register();

            StartWatchdog();
        }

        private async void StartWatchdog()
        {
            if (_isRunning) return;
            _isRunning = true;

            while (_isRunning)
            {
                await Task.Delay(5000);
                
                try
                {
                    foreach (Player p in Nova.server.GetAllPlayers())
                    {
                        if (p == null || !p.IsAdmin || p.setup?.character == null) 
                            continue;

                        int id = p.setup.character.Id;
                        bool confirmed = _confirmedStates.ContainsKey(id) && _confirmedStates[id];
                        
                        if (p.IsAdminService != confirmed && !_isPanelActive.Contains(id))
                        {
                            await Task.Delay(1000);
                            
                            if (p.IsAdminService != confirmed && !_isPanelActive.Contains(id))
                            {
                                p.IsAdminService = confirmed;
                                Logger.LogWarning("ASN - Watchdog", $"Bypass detecte pour {p.account.username}");
                                ConfirmServiceToggle(p);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"ASN - Watchdog Error: {ex.Message}", "ASN");
                }
            }
        }

        public override void OnPlayerDisconnect(NetworkConnection conn)
        {
            base.OnPlayerDisconnect(conn);
            
            try
            {
                Player player = Nova.server.GetPlayer(conn);

                if (player?.IsAdmin == true && player.setup?.character != null)
                {
                    int id = player.setup.character.Id;

                    if (player.IsAdminService && _confirmedStates.ContainsKey(id) && _confirmedStates[id])
                    {
                        bool wasSilent = _silentMode.ContainsKey(id) && _silentMode[id];
                        _confirmedStates[id] = false;
                        string duration = StopTrackingAndGetDuration(player);
                        
                        _ = SendEmbedLog(
                            Config.AdminUseServiceAdminWebhookUrl, 
                            "#e67e22", 
                            "🟠 DECONNEXION EN SERVICE", 
                            player, 
                            duration,
                            wasSilent
                        );
                    }

                    _confirmedStates.Remove(id);
                    _serviceSessions.Remove(id);
                    _silentMode.Remove(id);
                    _isPanelActive.Remove(id);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"ASN - OnPlayerDisconnect Error: {ex.Message}", "ASN");
            }
        }

        public void ConfirmServiceToggle(Player player)
        {
            if (player?.setup?.character == null) return;

            int id = player.setup.character.Id;
            if (_isPanelActive.Contains(id)) return;
            _isPanelActive.Add(id);

            bool isInService = player.IsAdminService;
            
            Panel panel = PanelHelper.Create(
                isInService ? "Quitter le service ?" : "Prendre le service ?", 
                UIPanel.PanelType.Tab, 
                player, 
                () => ConfirmServiceToggle(player)
            );
            
            panel.AddButton("Fermer", ui => 
            { 
                _isPanelActive.Remove(id); 
                player.ClosePanel(panel); 
            });

            panel.AddButton("Valider", ui => ui.SelectTab());

            panel.AddTabLine("Non", ui => 
            { 
                _isPanelActive.Remove(id); 
                player.ClosePanel(panel); 
            });

            if (!isInService)
            {
                panel.AddTabLine("Oui (Visible)", async ui =>
                {
                    await HandleServiceToggle(player, id, panel, false);
                });

                panel.AddTabLine("Oui (Mode Silent)", async ui =>
                {
                    await HandleServiceToggle(player, id, panel, true);
                });
            }
            else
            {
                panel.AddTabLine("Oui", async ui =>
                {
                    await HandleServiceToggle(player, id, panel, false);
                });
            }

            player.ShowPanelUI(panel);
        }

        private async Task HandleServiceToggle(Player player, int id, Panel panel, bool silent)
        {
            _isPanelActive.Remove(id);
            player.ClosePanel(panel);

            try
            {
                bool isInService = player.IsAdminService;

                if (!isInService)
                {
                    player.IsAdminService = true;
                    _confirmedStates[id] = true;
                    _silentMode[id] = silent;
                    StartTracking(player);
                    
                    await SendEmbedLog(
                        Config.AdminUseServiceAdminWebhookUrl, 
                        silent ? "#9b59b6" : "#2ecc71", 
                        silent ? "🟣 PRISE DE SERVICE (SILENT)" : "🟢 PRISE DE SERVICE", 
                        player,
                        null,
                        silent
                    );
                    
                    if (!silent)
                    {
                        Nova.server.SendMessageToAll(
                            $"<color=#ff0202>[Serveur]</color> <color=#ffffff>L'Admin {player.account.username} est disponible</color>"
                        );
                    }
                    
                    Logger.LogSuccess("ASN - Service", $"{player.account.username} en service {(silent ? "(SILENT)" : "")}");
                }
                else
                {
                    bool wasSilent = _silentMode.ContainsKey(id) && _silentMode[id];
                    player.IsAdminService = false;
                    _confirmedStates[id] = false;
                    _silentMode[id] = false;
                    string duration = StopTrackingAndGetDuration(player);
                    
                    await SendEmbedLog(
                        Config.AdminUseServiceAdminWebhookUrl, 
                        "#e74c3c", 
                        "🔴 FIN DE SERVICE", 
                        player, 
                        duration,
                        wasSilent
                    );
                    
                    if (!wasSilent)
                    {
                        Nova.server.SendMessageToAll(
                            $"<color=#ff0202>[Serveur]</color> <color=#ffffff>L'Admin {player.account.username} est indisponible</color>"
                        );
                    }
                    
                    Logger.LogSuccess("ASN - Service", $"{player.account.username} hors service ({duration}) {(wasSilent ? "[ETAIT SILENT]" : "")}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"ASN - HandleServiceToggle Error: {ex.Message}", "ASN");
                player.Notify("Erreur", "Erreur lors du changement de service.", NotificationManager.Type.Error);
            }
        }

        private async Task SendEmbedLog(string url, string hexColor, string title, Player player, string duration = null, bool isSilent = false)
        {
            if (string.IsNullOrEmpty(url) || url == "URL_ICI") return;

            try
            {
                DiscordWebhookClient client = new DiscordWebhookClient(url);

                string steamId = player.account.steamId.ToString();
                string rpName = (player.setup?.character != null) 
                    ? $"{player.setup.character.Firstname} {player.setup.character.Lastname}" 
                    : "Inconnu";

                List<string> fieldNames = new List<string> 
                { 
                    "🎭 Nom RP", 
                    "🆔 ID Perso", 
                    "💤 Compte", 
                    "🎮 Nom Steam", 
                    "💾 SteamID", 
                    "🔗 Profil" 
                };

                List<string> fieldValues = new List<string> 
                { 
                    rpName, 
                    player.setup.character.Id.ToString(), 
                    player.account.username, 
                    player.steamUsername, 
                    steamId, 
                    $"[Cliquez ici](https://steamcommunity.com/profiles/{steamId})" 
                };

                if (isSilent)
                {
                    fieldNames.Add("🕵️ Mode");
                    fieldValues.Add("**SILENT** (Invisible aux joueurs)");
                }

                if (!string.IsNullOrEmpty(duration))
                {
                    fieldNames.Add("⏳ Duree du service");
                    fieldValues.Add($"**{duration}**");
                }

                await DiscordHelper.SendEmbed(
                    client, 
                    hexColor, 
                    title, 
                    "Log AdminServicesNotifier v2.1.0", 
                    fieldNames, 
                    fieldValues, 
                    false, 
                    true, 
                    $"Fait par Robocnop • {DateTime.Now:HH:mm}"
                );
            }
            catch (Exception ex)
            {
                Logger.LogError($"ASN - Discord Webhook Error: {ex.Message}", "ASN");
            }
        }

        private void StartTracking(Player player)
        {
            if (player?.setup?.character != null)
            {
                _serviceSessions[player.setup.character.Id] = DateTime.Now;
            }
        }

        private string StopTrackingAndGetDuration(Player player)
        {
            if (player?.setup?.character != null && _serviceSessions.ContainsKey(player.setup.character.Id))
            {
                TimeSpan duration = DateTime.Now - _serviceSessions[player.setup.character.Id];
                _serviceSessions.Remove(player.setup.character.Id);
                return $"{duration.Hours}h {duration.Minutes}m {duration.Seconds}s";
            }
            return "Inconnu";
        }

        public void InsertMenu()
        {
            _menu.AddAdminTabLine(
                PluginInformations, 
                1, 
                "AdminServicesNotifier", 
                (ui) => ConfirmServiceToggle(PanelHelper.ReturnPlayerFromPanel(ui))
            );

            _menu.AddAdminPluginTabLine(
                PluginInformations, 
                1, 
                "AdminServicesNotifier", 
                (ui) => ConfirmServiceToggle(PanelHelper.ReturnPlayerFromPanel(ui)), 
                0
            );
        }

        public void InsertInteractionPutAdminOn()
        {
            _menu.AddInteractionTabLine(PluginInformations, "Gestion du service admin", (ui) => 
            {
                Player p = PanelHelper.ReturnPlayerFromPanel(ui);
                if (p?.IsAdmin == true) 
                    ConfirmServiceToggle(p);
            });
        }

        public override async void OnPlayerSpawnCharacter(Player player, NetworkConnection conn, Characters character)
        {
            base.OnPlayerSpawnCharacter(player, conn, character);

            if (player?.IsAdmin == true && player.setup?.character != null)
            {
                int id = player.setup.character.Id;
                _confirmedStates[id] = false;
                _silentMode[id] = false;

                await Task.Delay(3000);

                if (player.setup?.character != null)
                {
                    ConfirmServiceToggle(player);
                    await SendEmbedLog(
                        Config.AdminLoginWebhookUrl, 
                        "#3498db", 
                        "🔵 CONNEXION ADMIN", 
                        player
                    );
                }
            }
        }
    }
}