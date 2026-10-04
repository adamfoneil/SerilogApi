using SerilogLevelApi;
using Serilog.Events;

namespace Blazor.RCL.Services;

public record LogLevelOverrideDto(
    int Id,
    string Name,
    string Level,
    DateTime? ExpiryUtc);

public record LogLevelOverrideRequest(
    string Name,
    string Level,
    DateTime? ExpiryUtc = null);

public class SerilogLevelService
{
    private readonly ILogLevelOverrides _overrides;

    public SerilogLevelService(ILogLevelOverrides overrides)
    {
        _overrides = overrides;
    }

    public async Task<IEnumerable<LogLevelOverrideDto>> GetActiveOverridesAsync()
    {
        var overrides = await _overrides.GetAsync();

        return overrides
            .Select((kvp, index) => new LogLevelOverrideDto(
                Id: index,
                Name: kvp.Key,
                Level: kvp.Value.Level.ToString(),
                ExpiryUtc: kvp.Value.ExpiresUtc))
            .ToList();
    }

    public async Task SetLogLevelOverrideAsync(LogLevelOverrideRequest request)
    {
        if (!Enum.TryParse<LogEventLevel>(request.Level, out var level))
        {
            throw new ArgumentException($"Invalid log level: {request.Level}");
        }

        TimeSpan? expiresAfter = null;
        if (request.ExpiryUtc.HasValue)
        {
            expiresAfter = request.ExpiryUtc.Value - DateTime.UtcNow;
        }

        await _overrides.SetAsync(request.Name, level, expiresAfter);
    }

    public async Task DeleteLogLevelOverrideAsync(int id)
    {
        var overrides = await _overrides.GetAsync();

        if (overrides.Count > id && id >= 0)
        {
            var key = overrides.Keys.ElementAt(id);
            await _overrides.RemoveAsync(key);
        }
    }
}
