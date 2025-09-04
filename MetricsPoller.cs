using System;
using System.Threading;
using System.Threading.Tasks;
using SuchByte.MacroDeck.Logging;
using SuchByte.MacroDeck.Variables;
using SuchByte.MacroDeck.Plugins;

namespace User.AfterburnerRtss;

internal sealed class MetricsPoller
{
    private readonly MacroDeckPlugin _plugin;
    private readonly TimeSpan _interval;
    private readonly AfterburnerReader _abReader = new();
    private readonly RtssReader _rtssReader = new();

    public MetricsPoller(MacroDeckPlugin plugin, TimeSpan interval)
    {
        _plugin = plugin;
        _interval = interval;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await RefreshOnceAsync(); }
            catch (Exception ex)
            {
                MacroDeckLogger.Warning(_plugin, typeof(MetricsPoller), $"Refresh failed: {ex.Message}");
            }
            try { await Task.Delay(_interval, ct); } catch { }
        }
    }

    public Task RefreshOnceAsync()
    {
        var abSensors = _abReader.TryReadSensors();
        if (abSensors != null)
        {
            foreach (var kv in abSensors)
            {
                var (name, value) = (kv.Key, kv.Value);
                if (value is long l)
                    VariableManager.SetValue(name, l, VariableType.Integer, _plugin, save: false);
                else
                    VariableManager.SetValue(name, value?.ToString() ?? string.Empty, VariableType.String, _plugin, save: false);
            }
        }

        var rtss = _rtssReader.TryRead();
        if (rtss != null)
        {
            foreach (var kv in rtss)
            {
                var (name, value) = (kv.Key, kv.Value);
                if (value is long l)
                    VariableManager.SetValue(name, l, VariableType.Integer, _plugin, save: false);
                else
                    VariableManager.SetValue(name, value?.ToString() ?? string.Empty, VariableType.String, _plugin, save: false);
            }
        }

        return Task.CompletedTask;
    }
}
