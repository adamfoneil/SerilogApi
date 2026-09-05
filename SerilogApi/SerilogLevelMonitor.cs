using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;

namespace SerilogApi;

/// <summary>
/// add as hosted service in your application
/// </summary>
public class SerilogLevelMonitor(
    LoggingLevelSwitch levelSwitch,
    ILogLevelStore levelStore,
    ILogger<SerilogLevelMonitor> logger) : BackgroundService
{
    private readonly LoggingLevelSwitch _levelSwitch = levelSwitch;
    private readonly ILogLevelStore _levelStore = levelStore;
    private readonly ILogger<SerilogLevelMonitor> _logger = logger;
    private readonly Dictionary<string, LogEventLevel> _previousLevels = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var levels = await _levelStore.GetLevelsAsync();
                var now = DateTime.UtcNow;

                // Process all levels from the store
                foreach (var (category, (level, expiresUtc)) in levels)
                {
                    // Check if this level has expired
                    var isExpired = expiresUtc.HasValue && expiresUtc.Value <= now;

                    if (!isExpired)
                    {
                        // Level is active - apply it
                        if (category == "Default")
                        {
                            // Capture previous level before first elevation
                            if (!_previousLevels.ContainsKey(category))
                            {
                                _previousLevels[category] = _levelSwitch.MinimumLevel;
                                _logger.LogInformation("Captured previous log level {PreviousLevel} for category {Category}", 
                                    _previousLevels[category], category);
                            }

                            if (_levelSwitch.MinimumLevel != level)
                            {
                                _levelSwitch.MinimumLevel = level;
                                _logger.LogInformation("Elevated log level to {Level} for category {Category}, expires at {ExpiresUtc}", 
                                    level, category, expiresUtc?.ToString() ?? "never");
                            }
                        }
                    }
                    else
                    {
                        // Level has expired - revert if we were tracking it
                        if (category == "Default" && _previousLevels.ContainsKey(category))
                        {
                            var previousLevel = _previousLevels[category];
                            _levelSwitch.MinimumLevel = previousLevel;
                            _previousLevels.Remove(category);
                            _logger.LogInformation("Reverted log level to {PreviousLevel} for expired category {Category}", 
                                previousLevel, category);
                        }
                    }
                }

                // Check for categories that were removed from the store (manual removal or cleanup)
                var trackedCategories = _previousLevels.Keys.ToList();
                foreach (var trackedCategory in trackedCategories)
                {
                    if (!levels.ContainsKey(trackedCategory))
                    {
                        // Category was removed from store - revert it
                        if (trackedCategory == "Default")
                        {
                            var previousLevel = _previousLevels[trackedCategory];
                            _levelSwitch.MinimumLevel = previousLevel;
                            _previousLevels.Remove(trackedCategory);
                            _logger.LogInformation("Reverted log level to {PreviousLevel} for removed category {Category}", 
                                previousLevel, trackedCategory);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling log level store");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
