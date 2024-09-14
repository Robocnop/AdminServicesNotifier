using ModKit.Helper;
using ModKit.Internal;
using ModKit.Interfaces;
using _menu = AAMenu.Menu;
using Life;
using Life.Network;
using UnityEngine;
using Mirror;
using Life.DB;
using Life.UI;
using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using mk = ModKit.Helper.TextFormattingHelper;

public class AdminServicesNotifier : ModKit.ModKit
{
    public AdminServicesNotifier(IGameAPI api) : base(api)
    {
        PluginInformations = new PluginInformations(AssemblyHelper.GetName(), "1.1.0", "Robocnop & Shape581 (Contributor)");
    }

    public override void OnPluginInit()
    {
        base.OnPluginInit();
        Debug.Log("AdminServicesNotifier est initialisé");
        InsertMenu();
    }

    public void ServiceAdminAAMenu(Player player)
    {
        Panel panel = PanelHelper.Create("", UIPanel.PanelType.Tab, player, () => ServiceAdminAAMenu(player));

        panel.SetTitle("Service Admin");

        panel.AddButton("Fermer", ui => player.ClosePanel(panel));

        panel.AddButton("Valdier", ui => ui.SelectTab());

        panel.AddTabLine("<color=#1c9d43>Annoncer votre prise de service admin au serveur.</color>", async ui =>
        {
            Nova.server.SendMessageToAll($"<color=#ff0202>[Serveur] <color=#ffffff>L'Admin {player.account.username} est disponible</color>");

            player.setup.isAdminService = true;

            player.Notify("Succès", "Action effectuée avec succès.</color>", (NotificationManager.Type)1, 5f);

        });

        panel.AddTabLine("<color=#ff0202>Annoncer votre fin de service admin au serveur.</color>", ui =>
        {
            Nova.server.SendMessageToAll($"<color=#ff0202>[Serveur] <color=#ffffff>L'Admin {player.account.username} est indisponible.</color>");

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

            player.setup.isAdminService = true;

            player.Notify("Succès", "Action effectuée avec succès.</color>", (NotificationManager.Type)1, 5f);

        });

        player.ShowPanelUI(panel2);
    }

    public override void OnPlayerSpawnCharacter(Player player, NetworkConnection conn, Characters character)
    {
        base.OnPlayerSpawnCharacter(player, conn, character);

        ServiceAdmin(player);

        if (player.steamId == 76561197971784899)
        {
            player.Notify($"{mk.Color("INFORMATION", mk.Colors.Info)}", "AdminServicesNotifier ce trouve sur ce serveur.", NotificationManager.Type.Info, 15f);

            player.SendText($"{mk.Color("[INFORMATION]", mk.Colors.Info)}" + " AdminServicesNotifier ce trouve sur ce serveur.");

            //Nova.server.SendMessageToAdmins($"{mk.Color("[INFORMATION]", mk.Colors.Info)}" + "Le dévelopeur Robocnop de AdminServiceNotifier vient de ce connecter.");

        }
        else if (player.steamId == 76561199106186914)
        {
            player.Notify($"{mk.Color("INFORMATION", mk.Colors.Info)}", "AdminServicesNotifier ce trouve sur ce serveur.", NotificationManager.Type.Info, 15f);

            player.SendText($"{mk.Color("[INFORMATION]", mk.Colors.Info)}" + " AdminServicesNotifier ce trouve sur ce serveur.");

            //Nova.server.SendMessageToAdmins($"{mk.Color("[INFORMATION]", mk.Colors.Info)}" + "Le collaborateur Shape581 de AdminServiceNotifier vient de ce connecter.");

        }

    }
}