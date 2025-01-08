using ModKit.Helper;
using ModKit.Interfaces;
using ModKit.Internal;
using _menu = AAMenu.Menu;
using Life;
using Life.Network;
using UnityEngine;
using Mirror;
using Life.DB;
using Life.UI;
using mk = ModKit.Helper.TextFormattingHelper;
using System.Diagnostics;
using ModKit.Helper.DiscordHelper;
using System.IO;
using System.Reflection;
using System;

public class AdminServicesNotifier : ModKit.ModKit
{
    public Config config { get; private set; }

    public AdminServicesNotifier(IGameAPI api) : base(api)
    {
        PluginInformations = new PluginInformations(AssemblyHelper.GetName(), "1.3.0", "Robocnop & Shape581 (Contributor)");
    }

    public async override void OnPluginInit()
    {
        base.OnPluginInit();

        ModKit.Internal.Logger.LogSuccess($"{PluginInformations.SourceName} v{PluginInformations.Version}", "initialisé");

        InsertMenu();
        CreateConfig();
        InsertInetractionPutAdminOn();

        // Nuh-uh, no webhooks for you :(

    }

    public void ServiceAdminAAMenu(Player player)
    {
        Panel panel = PanelHelper.Create("", UIPanel.PanelType.Tab, player, () => ServiceAdminAAMenu(player));

        panel.SetTitle($"Service Admin");

        panel.AddButton("Fermer", ui => player.ClosePanel(panel));

        panel.AddButton("Valdier", ui => ui.SelectTab());

        panel.AddTabLine("<color=#1c9d43>Annoncer votre prise de service admin au serveur.</color>", ui =>
        {
            if (!string.IsNullOrEmpty(config.AdminUseServiceAdminWebhookUrl))
            {
                DiscordWebhookClient serviceAdminUseServiceAdminWebhookClient = new DiscordWebhookClient(config.AdminUseServiceAdminWebhookUrl);
                DiscordHelper.SendMsg(serviceAdminUseServiceAdminWebhookClient, $"[SERVICE ADMIN = ON] L'Admin **{player.account.username}** a pris son service admin le **{DateTime.Now}** en utilisant le panel de AdminServicesNotifier.");
            }

            Nova.server.SendMessageToAll($"<color=#ff0202>[Serveur] <color=#ffffff>L'Admin {player.account.username} est disponible</color>");

            player.setup.isAdminService = true;

            player.Notify("Succès", "Action effectuée avec succès.</color>", (NotificationManager.Type)1, 5f);

        });

        panel.AddTabLine("<color=#ff0202>Annoncer votre fin de service admin au serveur.</color>", ui =>
        {
            if (!string.IsNullOrEmpty(config.AdminUseServiceAdminWebhookUrl))
            {
                DiscordWebhookClient serviceAdminUseServiceAdminWebhookClient = new DiscordWebhookClient(config.AdminUseServiceAdminWebhookUrl);
                DiscordHelper.SendMsg(serviceAdminUseServiceAdminWebhookClient, $"[SERVICE ADMIN = OFF] L'Admin **{player.account.username}** a a arrêté son service admin le **{DateTime.Now}** en utilisant le panel de AdminServicesNotifier.");
            }

            Nova.server.SendMessageToAll($"<color=#ff0202>[Serveur] <color=#ffffff>L'Admin {player.account.username} est indisponible</color>");

            player.setup.isAdminService = false;

            player.Notify("Succès", "Action effectuée avec succès.</color>", (NotificationManager.Type)1, 5f);

        });

        player.ShowPanelUI(panel);
    }

    public void InsertMenu()
    {
        _menu.AddAdminTabLine(PluginInformations, 1, "AdminServicesNotifier", (ui) =>
        {
            Player player = PanelHelper.ReturnPlayerFromPanel(ui);

            ServiceAdminAAMenu(player);
        });

        _menu.AddAdminPluginTabLine(PluginInformations, 1, "AdminServicesNotifier", (ui) =>
        {
            Player player = PanelHelper.ReturnPlayerFromPanel(ui);

        }, 0);
    }

    public void ServiceAdmin(Player player)
    {
        Panel panel2 = PanelHelper.Create("AdminServicesNotifier", UIPanel.PanelType.Tab, player, () => ServiceAdmin(player));

        panel2.SetTitle("Prendre son service admin");

        panel2.AddButton("Fermer", ui => player.ClosePanel(panel2));

        panel2.AddButton("Valider", ui => ui.SelectTab());

        panel2.AddTabLine("Non", ui =>
        {
            player.ClosePanel(panel2);

        });

        panel2.AddTabLine("Oui", async ui =>
        {
            player.ClosePanel(panel2);

            Nova.server.SendMessageToAll($"<color=#ff0202>[Serveur] <color=#ffffff>L'Admin {player.account.username} est disponible</color>");

            player.SetAdminService(true);

            player.Notify("Succès", "Action effectuée avec succès.</color>", (NotificationManager.Type)1, 5f);

            if (!string.IsNullOrEmpty(config.AdminUseServiceAdminWebhookUrl))
            {
                DiscordWebhookClient serviceAdminUseServiceAdminWebhookClient = new DiscordWebhookClient(config.AdminUseServiceAdminWebhookUrl);
                await DiscordHelper.SendMsg(serviceAdminUseServiceAdminWebhookClient, $"[SERVICE ADMIN = ON] L'Admin **{player.account.username}** a pris son service admin le **{DateTime.Now}** en ce connectant sur le serveur.");
            }

        });

        player.ShowPanelUI(panel2);
    }

    public void InsertInetractionPutAdminOn()
    {
        _menu.AddInteractionTabLine(PluginInformations, "Ce mettre admin et l'annoncer au serveur", async (ui) =>
        {
            Player player = PanelHelper.ReturnPlayerFromPanel(ui);

            if (player.IsAdmin)
            {
                if (!string.IsNullOrEmpty(config.AdminUseServiceAdminWebhookUrl))
                {
                    DiscordWebhookClient serviceAdminUseServiceAdminWebhookClient = new DiscordWebhookClient(config.AdminUseServiceAdminWebhookUrl);
                    await DiscordHelper.SendMsg(serviceAdminUseServiceAdminWebhookClient, $"[SERVICE ADMIN = ON] L'Admin **{player.account.username}** a pris son service admin le **{DateTime.Now}** dans le menu interaction.");
                }

                player.SetAdminService(true);
                player.Notify("Succès", "Action effectuée avec succès.</color>", (NotificationManager.Type)1, 5f);
            }
            else
            {
                player.Notify("Erreur", "Vous n'êtes pas admin.</color>", (NotificationManager.Type)1, 5f);
            }
        });
    }

    public override void OnPlayerSpawnCharacter(Player player, NetworkConnection conn, Characters character)
    {
        base.OnPlayerSpawnCharacter(player, conn, character);

        if (player.IsAdmin)
        {

            ServiceAdmin(player);
            if (!string.IsNullOrEmpty(config.AdminLoginWebhookUrl))
            {
                DiscordWebhookClient adminLoginWebhookClient = new DiscordWebhookClient(config.AdminLoginWebhookUrl);
                DiscordHelper.SendMsg(adminLoginWebhookClient, $"# [ADMINSERVICESNOTIFIER]" +
                     $"\n**Un admin s'est connecté au serveur !**" +
                     $"\nL'Admin **{player.account.username}** s'est connecté au serveur." +
                     $"\n{player.account.username} s'est connecté le **{DateTime.Now}** sur le serveur."
                     );
            }

        }

        if (player.steamId == 76561197971784899)
        {
            player.Notify($"{mk.Color("INFORMATION", mk.Colors.Info)}", "AdminServicesNotifier ce trouve sur ce serveur.", NotificationManager.Type.Info, 15f);

            player.SendText($"{mk.Color("[INFORMATION]", mk.Colors.Info)}" + " AdminServicesNotifier ce trouve sur ce serveur.");

            if (config.Crédits == "true")
            {
                Nova.server.SendMessageToAdmins($"{mk.Color("[INFORMATION]", mk.Colors.Info)}" + " Le dévelopeur Robocnop de AdminServicesNotifier vient de ce connecter.");
            }

        }
        else if (player.steamId == 76561199106186914)
        {
            player.Notify($"{mk.Color("INFORMATION", mk.Colors.Info)}", "AdminServicesNotifier ce trouve sur ce serveur.", NotificationManager.Type.Info, 15f);

            player.SendText($"{mk.Color("[INFORMATION]", mk.Colors.Info)}" + " AdminServicesNotifier ce trouve sur ce serveur.");

            if (config.Crédits == "true")
            {
                Nova.server.SendMessageToAdmins($"{mk.Color("[INFORMATION]", mk.Colors.Info)}" + " Le collaborateur Shape581 de AdminServicesNotifier vient de ce connecter.");
            }

        }

    }
    public static string GetAssemblyName()
    {
        return Assembly.GetCallingAssembly().GetName().Name;
    }

    public void CreateConfig()
    {
        string directoryPath = pluginsPath + $"/{GetAssemblyName()}";
        string configFilePath = directoryPath + "/config.json";

        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        if (!File.Exists(configFilePath))
        {
            var defaultConfig = new Config
            {
                Crédits = "true", // true = activer, false = désactiver
                AdminLoginWebhookUrl = "https://discord.com/api/webhooks/adminlogin",
                AdminUseServiceAdminWebhookUrl = "https://discord.com/api/webhooks/adminuseserviceadmin"
            };
            string jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(defaultConfig, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(configFilePath, jsonContent);
        }

        config = Newtonsoft.Json.JsonConvert.DeserializeObject<Config>(File.ReadAllText(configFilePath));
    }

}
