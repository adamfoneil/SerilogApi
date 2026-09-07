using SerilogApi;

namespace QueryApi.MySql;

public record JsonColumn(
    LogTableColumns Column, // usually the JsonProperties column, but could be Message column when it has json
    string Alias, 
    string Expression);

public class MySqlLogQuery(string connectionString, ColumnConfiguration columnConfig) : ILogQuery
{
    private readonly string _connectionString = connectionString;
    private readonly ColumnConfiguration _columnConfig = columnConfig;

    private string BuildQuery(JsonColumn[] concatExpressions, LogCriteria criteria) => 
        $"SELECT {ColumnNames(concatExpressions)} FROM `{_columnConfig.TableName}`";

    private string ColumnNames(JsonColumn[] concatExpressions) => 
        string.Join(", ", 
            _columnConfig.ColumnMappings.Select(col => $"`{col.Value}`")
            .Concat(concatExpressions.Select(expr => ExtractPropertyExpression(expr))));

    private string ExtractPropertyExpression(JsonColumn jsonColumn) => $"`{_columnConfig.ColumnMappings[jsonColumn.Column]}`->>'{jsonColumn.Expression}' AS `{jsonColumn.Alias}`";

    public Task<LogEntry[]> QueryAsync(LogCriteria filter)
    {
        throw new NotImplementedException();
    }

    public Task<ErrorInfo[]> RecentErrorsAsync(string? dateTimeExpression = null)
    {
        throw new NotImplementedException();
    }

    public Task<LogEntry[]> TraceAsync(string requestId)
    {
        throw new NotImplementedException();
    }
}
