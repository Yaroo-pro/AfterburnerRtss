using System;
using System.Collections.Generic;
using System.Drawing;
using SuchByte.MacroDeck.Logging;
using SuchByte.MacroDeck.Plugins;

namespace User.AfterburnerRtss;

public sealed class Main : MacroDeckPlugin
{
    private MetricsPoller? _poller;
    public override bool CanConfigure => false;
    public override Image Icon => null;

    public override void Enable()
    {
        try
        {
            _poller = new MetricsPoller(this, TimeSpan.FromMilliseconds(1000));
            _ = _poller.RunAsync(System.Threading.CancellationToken.None);
            MacroDeckLogger.Info(this, typeof(Main), "Metrics poller started");
        }
        catch (Exception ex)
        {
            MacroDeckLogger.Error(this, typeof(Main), $"Failed to start poller: {ex.Message}");
        }

        this.Actions = new List<PluginAction> { new RefreshNowAction(this) };
    }

    internal void ManualRefresh() => _ = _poller?.RefreshOnceAsync();
}
