using Microsoft.Extensions.Configuration;
using Serilog.Events;

namespace SerilogLevelApi;

public record LogLevelOverride(
    LogEventLevel Level,
    DateTime? ExpiresUtc);

/// <summary>
/// describes how to read and write log level changes across horizontally-scaled instances of an app
/// </summary>
public interface ILogLevelOverrides
{
    Task<Dictionary<string, LogLevelOverride>> GetAsync();
    Task SetAsync(string category, LogEventLevel level, TimeSpan? expiresAfter = null);
    Task RemoveAsync(string category);
}
