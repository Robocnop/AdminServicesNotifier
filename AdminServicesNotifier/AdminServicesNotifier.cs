using System;
using System.Collections.Concurrent;
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

        // ConcurrentDictionary : les callbacks async peuvent reprendre hors du thread principal
        private readonly ConcurrentDictionary<int, DateTime> _serviceSessions = new ConcurrentDictionary<int, DateTime>();
        private readonly ConcurrentDictionary<int, bool> _confirmedStates = new ConcurrentDictionary<int, bool>();
        private readonly ConcurrentDictionary<int, bool> _silentMode = new ConcurrentDictionary<int, bool>();
        private readonly ConcurrentDictionary<int, DateTime> _panelOpenedAt = new ConcurrentDictionary<int, DateTime>();

        // Un panel fermé via Echap ne déclenche aucun callback : sans expiration, l'admin resterait bloqué
        private static readonly TimeSpan PanelTimeout = TimeSpan.FromMinutes(2);

        private bool _isRunning = false;

        public AdminServicesNotifier(IGameAPI api) : base(api)
        {
            PluginInformations = new PluginInformations(AssemblyHelper.GetName(), "2.2.0", "Robocnop");
        }

        public override void OnPluginInit()
        {
            base.OnPluginInit();
            Logger.LogSuccess("ASN - Demarrage", $"v{PluginInformations.Version} initialise");

            Config = ASNConfigHandler.LoadConfig(pluginsPath, PluginInformations.Version);

            if (!IsWebhookConfigured(Config.AdminLoginWebhookUrl) && !IsWebhookConfigured(Config.AdminUseServiceAdminWebhookUrl))
                Logger.LogWarning("ASN - Config", "Aucun webhook Discord configure, les logs Discord sont desactives.");

            if (Config.CheckForUpdates)
                _ = UpdateChecker.CheckAsync(PluginInformations.Version);

            InsertMenu();
            InsertInteractionPutAdminOn();

            new SChatCommand("/a", "Voir les admins en service", "/a", (player, args) =>
            {
                if (player == null) return;

                if (Config.AllowPlayerToSeeAdmin || player.IsAdmin)
                {
                    List<string> adminsOnDuty = new List<string>();
                    foreach (Player p in Nova.server.GetAllPlayers())
                    {
                        if (p == null || !p.IsAdminService || p.setup?.character == null || p.account == null)
                            continue;

                        bool isSilent = _silentMode.TryGetValue(p.setup.character.Id, out bool silent) && silent;

                        if (!isSilent)
                            adminsOnDuty.Add(p.account.username);
                        else if (player.IsAdmin)
                            adminsOnDuty.Add($"{p.account.username} (silencieux)");
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
                if (player == null) return;

                if (player.IsAdmin)
                    ConfirmServiceToggle(player);
                else
                    player.Notify("Erreur", "Commande reservee aux admins.", NotificationManager.Type.Error);
            }).Register();

            StartWatchdog();
        }

        private static bool IsWebhookConfigured(string url)
        {
            return !string.IsNullOrWhiteSpace(url)
                && url != "URL_ICI"
                && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        private async void StartWatchdog()
        {
            if (_isRunning) return;
            _isRunning = true;

            while (_isRunning)
            {
                try
                {
                    await Task.Delay(5000);

                    foreach (Player p in Nova.server.GetAllPlayers())
                    {
                        if (p == null || !p.IsAdmin || p.setup?.character == null)
                            continue;

                        int id = p.setup.character.Id;
                        bool confirmed = _confirmedStates.TryGetValue(id, out bool state) && state;

                        if (p.IsAdminService != confirmed && !IsPanelActive(id))
                        {
                            await Task.Delay(1000);

                            // Re-verification : le joueur a pu se deconnecter ou valider entre-temps
                            if (p.setup?.character == null)
                                continue;

                            confirmed = _confirmedStates.TryGetValue(id, out state) && state;

                            if (p.IsAdminService != confirmed && !IsPanelActive(id))
                            {
                                p.IsAdminService = confirmed;
                                Logger.LogWarning("ASN - Watchdog", $"Bypass detecte pour {p.account?.username ?? "inconnu"}");
                                ConfirmServiceToggle(p);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("ASN - Watchdog", ex.Message);
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

                    if (player.IsAdminService && _confirmedStates.TryGetValue(id, out bool confirmed) && confirmed)
                    {
                        bool wasSilent = _silentMode.TryGetValue(id, out bool silent) && silent;
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

                    _confirmedStates.TryRemove(id, out _);
                    _serviceSessions.TryRemove(id, out _);
                    _silentMode.TryRemove(id, out _);
                    _panelOpenedAt.TryRemove(id, out _);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("ASN - OnPlayerDisconnect", ex.Message);
            }
        }

        private bool IsPanelActive(int id)
        {
            if (!_panelOpenedAt.TryGetValue(id, out DateTime openedAt))
                return false;

            if (DateTime.Now - openedAt < PanelTimeout)
                return true;

            // Panel abandonne (ferme via Echap) : on debloque
            _panelOpenedAt.TryRemove(id, out _);
            return false;
        }

        public void ConfirmServiceToggle(Player player)
        {
            if (player?.setup?.character == null) return;

            int id = player.setup.character.Id;
            if (IsPanelActive(id)) return;
            _panelOpenedAt[id] = DateTime.Now;

            bool isInService = player.IsAdminService;

            Panel panel = PanelHelper.Create(
                isInService ? "Quitter le service ?" : "Prendre le service ?",
                UIPanel.PanelType.Tab,
                player,
                () =>
                {
                    _panelOpenedAt.TryRemove(id, out _);
                    ConfirmServiceToggle(player);
                }
            );

            panel.AddButton("Fermer", ui =>
            {
                _panelOpenedAt.TryRemove(id, out _);
                player.ClosePanel(panel);
            });

            panel.AddButton("Valider", ui => ui.SelectTab());

            panel.AddTabLine("Non", ui =>
            {
                _panelOpenedAt.TryRemove(id, out _);
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
            _panelOpenedAt.TryRemove(id, out _);
            player.ClosePanel(panel);

            try
            {
                // Le joueur a pu se deconnecter pendant que le panel etait ouvert
                if (player?.setup?.character == null || player.account == null) return;

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
                    bool wasSilent = _silentMode.TryGetValue(id, out bool s) && s;
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
                Logger.LogError("ASN - HandleServiceToggle", ex.Message);
                player?.Notify("Erreur", "Erreur lors du changement de service.", NotificationManager.Type.Error);
            }
        }

        private async Task SendEmbedLog(string url, string hexColor, string title, Player player, string duration = null, bool isSilent = false)
        {
            if (!IsWebhookConfigured(url)) return;
            if (player?.account == null) return;

            try
            {
                DiscordWebhookClient client = new DiscordWebhookClient(url);

                string steamId = player.account.steamId.ToString();
                string rpName = (player.setup?.character != null)
                    ? $"{player.setup.character.Firstname} {player.setup.character.Lastname}"
                    : "Inconnu";
                string characterId = (player.setup?.character != null)
                    ? player.setup.character.Id.ToString()
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
                    characterId,
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
                    $"Log AdminServicesNotifier v{PluginInformations.Version}",
                    fieldNames,
                    fieldValues,
                    false,
                    true,
                    $"Fait par Robocnop • {DateTime.Now:HH:mm}"
                );
            }
            catch (Exception ex)
            {
                Logger.LogError("ASN - Discord Webhook", ex.Message);
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
            if (player?.setup?.character != null && _serviceSessions.TryRemove(player.setup.character.Id, out DateTime start))
            {
                TimeSpan duration = DateTime.Now - start;
                return $"{(int)duration.TotalHours}h {duration.Minutes}m {duration.Seconds}s";
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

            try
            {
                if (player?.IsAdmin != true || player.setup?.character == null) return;

                int id = player.setup.character.Id;

                // Ne pas reinitialiser l'etat sur un respawn : cela couperait le service
                // en cours et le watchdog le detecterait comme un bypass
                bool alreadyTracked = _confirmedStates.ContainsKey(id);
                if (alreadyTracked) return;

                _confirmedStates[id] = false;
                _silentMode[id] = false;

                await Task.Delay(3000);

                // Le joueur a pu se deconnecter pendant le delai
                if (player.setup?.character == null) return;

                if (Config.OpenPanelOnSpawn)
                    ConfirmServiceToggle(player);

                await SendEmbedLog(
                    Config.AdminLoginWebhookUrl,
                    "#3498db",
                    "🔵 CONNEXION ADMIN",
                    player
                );
            }
            catch (Exception ex)
            {
                Logger.LogError("ASN - OnPlayerSpawnCharacter", ex.Message);
            }
        }
    }
}
