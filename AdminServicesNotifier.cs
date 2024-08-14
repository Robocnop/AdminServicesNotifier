using ModKit.Helper;
using ModKit.Internal;
using ModKit.Interfaces;
using _menu = AAMenu.Menu;
using Life;
using Life.Network;
using UnityEngine;

public class AdminServicesNotifier : ModKit.ModKit
{
    public AdminServicesNotifier(IGameAPI api) : base(api)
    {
        PluginInformations = new PluginInformations(AssemblyHelper.GetName(), "1.0.0", "Robocnop");
    }

    public override void OnPluginInit()
    {
        base.OnPluginInit();
        Debug.Log("AdminServicesNotifier est initialisé");
        InsertMenu();
    }

    public void InsertMenu()
    {
        _menu.AddAdminTabLine(PluginInformations, 1, "<color=#1c9d43>Annoncer votre prise de service admin au serveur", (ui) =>
        {
            Player player = PanelHelper.ReturnPlayerFromPanel(ui);

            Nova.server.SendMessageToAll($"<color=#ff0202>[Serveur] <color=#ffffff>L'Admin {player.account.username} est disponible");
            player.Notify("Succès", "Action effectuée avec succès.", (NotificationManager.Type)1, 5f);

        });

        _menu.AddAdminTabLine(PluginInformations, 1, "<color=#ff0202>Annoncer votre fin de prise de service admin au serveur", (ui) =>
        {
            Player player = PanelHelper.ReturnPlayerFromPanel(ui);

            Nova.server.SendMessageToAll($"<color=#ff0202>[Serveur] <color=#ffffff>L'Admin {player.account.username} est indisponible");
            player.Notify("Succès", "Action effectuée avec succès.", (NotificationManager.Type)1, 5f);

        });
    }
}