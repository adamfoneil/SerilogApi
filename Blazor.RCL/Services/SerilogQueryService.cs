using SerilogQueryApi;

namespace Blazor.RCL.Services;

public record LogQueryRequest(
    string? SearchText = null,
    string? MinLogLevel = null,
    int MaxResults = 100);

public record LogQueryResult(
    DateTime? Timestamp,
    string? Level,
    string? MessageTemplate,
    string? Message,
    string? SourceContext,
    string? RequestId,
    string? Exception);

public class SerilogQueryService(ILogQuery query)
{
    private readonly ILogQuery _query = query;

    public async Task<IEnumerable<LogQueryResult>> QueryLogsAsync(LogQueryRequest request)
    {
        var criteria = new LogCriteria
        {
            MessageContains = request.SearchText,
            Level = request.MinLogLevel,
            Take = request.MaxResults
        };

        var entries = await _query.QueryAsync(criteria);

        return entries.Select(e => new LogQueryResult(
            Timestamp: e.Timestamp,
            Level: e.Level,
            MessageTemplate: e.MessageTemplate,
            Message: e.Message,
            SourceContext: e.SourceContext,
            RequestId: e.RequestId,
            Exception: null));
    }
}
