using Microsoft.Extensions.Configuration;
using Serilog.Events;

namespace SerilogLevelApi;

/// <summary>
/// describes how to read and write log level changes across horizontally-scaled instances of an app
/// </summary>
public interface ILogLevelStore
{
    /// <summary>
    /// load baseline/default state from configuration
    /// </summary>
    Task InitializeAsync(IConfiguration configuration);
    /// <summary>
    /// used by public API to inspect current log levels
    /// </summary>    
    Task<Dictionary<string, (LogEventLevel Level, DateTime? ExpiresUtc)>> GetLevelsAsync();
    /// <summary>
    /// set the log level for a category with optional expiration when it reverts to baseline value
    /// </summary>
    Task SetLevelAsync(string category, LogEventLevel level, TimeSpan? expiresAfter = null);
}
