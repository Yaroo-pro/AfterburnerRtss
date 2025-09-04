using SuchByte.MacroDeck.ActionButton;
using SuchByte.MacroDeck.Plugins;

namespace User.AfterburnerRtss;

public sealed class RefreshNowAction : PluginAction
{
    private readonly Main _plugin;
    public RefreshNowAction(Main plugin) => _plugin = plugin;

    public override string Name => "Afterburner/RTSS: odśwież teraz";
    public override string Description => "Wymuś natychmiastowy odczyt i aktualizację zmiennych";

    public override void Trigger(string clientId, ActionButton actionButton) => _plugin.ManualRefresh();
}
