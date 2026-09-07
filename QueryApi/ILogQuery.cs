namespace SerilogQueryApi;

public enum SortOptions
{
    TimestampAsc, 
    TimestampDesc
}

public record ErrorInfo(
    // age of most recent RequestId
    TimeSpan Age,
    string SourceContext,
    string MessageTemplate,
    // most recent RequestIds
    string[] RequestIds);

public record LogEntry(
    DateTime Timestamp,
    string SourceContext,
    string Level,
    string MessageTemplate,
    string Message,
    string JsonData)
{
    public Dictionary<string, string> Properties { get; set; } = [];
    public TimeSpan Age { get; set; }
}

public record JsonColumn(
    LogTableColumns Column, // usually the JsonProperties column, but could be Message column when it has json
    string Alias,
    string Expression);

public class LogCriteria
{
    /// <summary>
    /// allows values like 
    /// "30m" = within 30 minutes ago. m = minutes ago, h = hours ago, d = days ago
    /// "3-4h" = between 3 and 4 hours ago
    /// "3:15am" = a specific time plus/minus 5 minutes (default before/after margin)
    /// "4:27pm*3" = a specific time plus/minus 3 minute margin before/after
    /// </summary>
    public string? DateTimeExpression { get; set; }
    public string? Level { get; set; }
    public string? MessageContains { get; set; }
    public string? MessageExcludes { get; set; }
    /// <summary>
    /// used when the message payload itself is json, and we're inspecting parts of the message
    /// </summary>
    public Dictionary<string, string> MessageProperties { get; set; } = [];
    /// <summary>
    /// typical structured log properties
    /// </summary>
    public Dictionary<string, string> Properties { get; set; } = [];
    /// <summary>
    /// zero = no limit
    /// </summary>
    public int MaxResults { get; set; } = 10;
}

public interface ILogQuery
{
    Task<ErrorInfo[]> RecentErrorsAsync(string? dateTimeExpression = null);

    Task<LogEntry[]> TraceAsync(string requestId, JsonColumn[] concatColumns);

    Task<LogEntry[]> QueryAsync(LogCriteria filter, JsonColumn[] concatColumns, SortOptions sortOptions = SortOptions.TimestampDesc);
}
