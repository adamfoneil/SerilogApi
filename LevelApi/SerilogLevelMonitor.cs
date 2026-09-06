using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;

namespace SerilogLevelApi;

/// <summary>
/// polls the overrides levelStore and applies any change, also reverts back to defined baseline when elevation expires
/// </summary>
public class SerilogLevelMonitor(
    LoggingLevelSwitch levelSwitch,
    ILogLevelOverrides levelStore,
    ILogger<SerilogLevelMonitor> logger) : BackgroundService
{
    private const string DefaultCategory = "Default";

    private readonly LoggingLevelSwitch _levelSwitch = levelSwitch;
    private readonly ILogLevelOverrides _levelStore = levelStore;
    private readonly ILogger<SerilogLevelMonitor> _logger = logger;
    private readonly LogEventLevel _configuredDefaultLevel = levelSwitch.MinimumLevel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var levels = await _levelStore.GetAsync();
                var now = DateTime.UtcNow;

                var hasDefaultOverride = levels.TryGetValue(DefaultCategory, out var defaultOverride);
                var defaultOverrideIsActive = hasDefaultOverride
                    && (!defaultOverride!.ExpiresUtc.HasValue || defaultOverride.ExpiresUtc.Value > now);

                var targetLevel = defaultOverrideIsActive
                    ? defaultOverride!.Level
                    : _configuredDefaultLevel;

                if (_levelSwitch.MinimumLevel != targetLevel)
                {
                    _levelSwitch.MinimumLevel = targetLevel;

                    if (defaultOverrideIsActive)
                    {
                        _logger.LogInformation(
                            "Applied log level override {Level} for category {Category}, expires at {ExpiresUtc}",
                            targetLevel,
                            DefaultCategory,
                            defaultOverride!.ExpiresUtc?.ToString() ?? "never");
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Reset log level to configured default {Level} for category {Category}",
                            _configuredDefaultLevel,
                            DefaultCategory);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling log level store");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
