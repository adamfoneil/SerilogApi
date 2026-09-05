namespace SerilogApi;

public record ErrorInfo(
    // age of most recent RequestId
    TimeSpan Age,
    string SourceContext,
    string MessageTemplate,
    // most recent RequestIds
    string[] RequestIds);

public record LogEntry(
    DateTime Timestamp,    
    TimeSpan Age,
    string SourceContext,
    string Level,
    string Message,
    Dictionary<string, string> Properties);

public class LogCriteria
{
    public string? DateTimeExpression { get; set; }
    public string? Level { get; set; }
    public string? SourceContext { get; set; }
    public string? MessageContains { get; set; }
    public string? MessageExcludes { get; set; }
    public Dictionary<string, string> MessageProperties { get; set; } = [];
    public Dictionary<string, string> Properties { get; set; } = [];
    /// <summary>
    /// zero = no limit
    /// </summary>
    public int MaxResults { get; set; } = 10;
}

public interface ILogQuery
{
    Task<ErrorInfo[]> RecentErrorsAsync(string? dateTimeExpression = null);

    Task<LogEntry[]> TraceAsync(string requestId);

    Task<LogEntry[]> QueryAsync(LogCriteria filter);
}
