namespace SerilogLevelSwitch.Query;
using System;

public abstract record DateFilter;

public record Last(TimeSpan Duration) : DateFilter;
public record Around(DateTime Center, TimeSpan Radius) : DateFilter;
public record Range(DateTime From, DateTime To) : DateFilter;
public record Since(DateTime From) : DateFilter;
public record Until(DateTime To) : DateFilter;

public static class DateFilterExtensions
{
    // Convert a DateFilter into an explicit start/end range. 'now' is used by Last and defaults to UTC now.
    public static (DateTime? Start, DateTime? End) ToRange(this DateFilter? filter, DateTime? now = null)
    {
        var nowVal = now ?? DateTime.UtcNow;
        return filter switch
        {
            Last(var duration) => (nowVal - duration, nowVal),
            Around(var center, var radius) => (center - radius, center + radius),
            Range(var from, var to) => (from, to),
            Since(var from) => (from, null),
            Until(var to) => (null, to),
            _ => (null, null)
        };
    }
}

public record SerilogFilter(
    DateFilter? DateFilter,
    string? Level,
    string? SourceContext,
    string? Message,
    Dictionary<string, string>? Properties = null);

public record SerilogEntry(
    DateTime Timestamp,
    string SourceContext,
    string Level,
    string Message,
    Dictionary<string, string> Properties);

public record SerilogSummary(    
    string SourceContext,
    string Level,
    int LogCount);

public interface ISerilogStore
{
    Task<SerilogEntry[]> QueryLogsAsync(SerilogFilter filter);
    Task<SerilogSummary[]> QuerySummaryAsync(DateFilter? dateFilter = null);
}
