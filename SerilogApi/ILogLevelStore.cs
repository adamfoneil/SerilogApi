using Serilog.Events;

namespace SerilogApi;

/// <summary>
/// describes how to read and write log level changes across horizontally-scaled instances of an app
/// </summary>
public interface ILogLevelStore
{
    Task<Dictionary<string, (LogEventLevel Level, DateTime? ExpiresUtc)>> GetLevelsAsync();
    Task SetLevelAsync(string category, LogEventLevel level, TimeSpan? expiresAfter = null);
}
