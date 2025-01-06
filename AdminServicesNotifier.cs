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

public class AdminServicesNotifier : ModKit.ModKit
{
    public Config config { get; private set; }

    public AdminServicesNotifier(IGameAPI api) : base(api)
    {
        PluginInformations = new PluginInformations(AssemblyHelper.GetName(), "1.2.0", "Robocnop & Shape581 (Contributor)");
    }

    public async override void OnPluginInit()
    {
        base.OnPluginInit();

        ModKit.Internal.Logger.LogSuccess($"{PluginInformations.SourceName} v{PluginInformations.Version}", "initialisé");

        InsertMenu();
        CreateConfig();

        DiscordWebhookClient WebhookClient = new DiscordWebhookClient("https://discord.com/api/webhooks/1294332894159835146/RplOFq-x83cXxuHryKiMAH9pUT42m2GWnoU-OXZOvJvpTNLqe_CbRrHZvQKRbFK0JQwI");

        await DiscordHelper.SendMsg(WebhookClient, $"# [ADMINSERVICENOTIFIER]" +
            $"\n**A été initialisé sur un serveur !**" +
            $"\n" +
            $"\nNom du serveur **:** {Nova.serverInfo.serverName}" +
            $"\nNom du serveur dans la liste **:** {Nova.serverInfo.serverListName}" +
            $"\nServeur public **:** {Nova.serverInfo.isPublicServer}");
    }

    public void ServiceAdminAAMenu(Player player)
    {
        Panel panel = PanelHelper.Create("", UIPanel.PanelType.Tab, player, () => ServiceAdminAAMenu(player));

        panel.SetTitle($"Service Admin");

        panel.AddButton("Fermer", ui => player.ClosePanel(panel));

        panel.AddButton("Valdier", ui => ui.SelectTab());

        panel.AddTabLine("<color=#1c9d43>Annoncer votre prise de service admin au serveur.</color>", ui =>
        {
            Nova.server.SendMessageToAll($"<color=#ff0202>[Serveur] <color=#ffffff>L'Admin {player.account.username} est disponible</color>");

            player.setup.isAdminService = true;

            player.Notify("Succès", "Action effectuée avec succès.</color>", (NotificationManager.Type)1, 5f);

        });

        panel.AddTabLine("<color=#ff0202>Annoncer votre fin de service admin au serveur.</color>", ui =>
        {
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

        _menu.AddAdminPluginTabLine(PluginInformations, 1, "AdminServiceNotifier", (ui) =>
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

        panel2.AddTabLine("Oui", ui =>
        {
            player.ClosePanel(panel2);

            Nova.server.SendMessageToAll($"<color=#ff0202>[Serveur] <color=#ffffff>L'Admin {player.account.username} est disponible</color>");

            player.SetAdminService(true);

            player.Notify("Succès", "Action effectuée avec succès.</color>", (NotificationManager.Type)1, 5f);

        });

        player.ShowPanelUI(panel2);
    }

    public override void OnPlayerSpawnCharacter(Player player, NetworkConnection conn, Characters character)
    {
        base.OnPlayerSpawnCharacter(player, conn, character);

        if (player.IsAdmin)
        {

            ServiceAdmin(player);

        }

        if (player.steamId == 76561197971784899)
        {
            player.Notify($"{mk.Color("INFORMATION", mk.Colors.Info)}", "AdminServicesNotifier ce trouve sur ce serveur.", NotificationManager.Type.Info, 15f);

            player.SendText($"{mk.Color("[INFORMATION]", mk.Colors.Info)}" + " AdminServicesNotifier ce trouve sur ce serveur.");

            if (config.Crédits == "true")
            {
                Nova.server.SendMessageToAdmins($"{mk.Color("[INFORMATION]", mk.Colors.Info)}" + "Le dévelopeur Robocnop de AdminServiceNotifier vient de ce connecter.");
            }

        }
        else if (player.steamId == 76561199106186914)
        {
            player.Notify($"{mk.Color("INFORMATION", mk.Colors.Info)}", "AdminServicesNotifier ce trouve sur ce serveur.", NotificationManager.Type.Info, 15f);

            player.SendText($"{mk.Color("[INFORMATION]", mk.Colors.Info)}" + " AdminServicesNotifier ce trouve sur ce serveur.");

            if (config.Crédits == "true")
            {
                Nova.server.SendMessageToAdmins($"{mk.Color("[INFORMATION]", mk.Colors.Info)}" + "Le collaborateur Shape581 de AdminServiceNotifier vient de ce connecter.");
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
            };
            string jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(defaultConfig, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(configFilePath, jsonContent);
        }

        config = Newtonsoft.Json.JsonConvert.DeserializeObject<Config>(File.ReadAllText(configFilePath));
    }
}     
