using SerilogApi;

namespace QueryApi.MySql;

public class MySqlLogQuery(string connectionString, ColumnConfiguration columnConfig) : ILogQuery
{
    private readonly string _connectionString = connectionString;
    private readonly ColumnConfiguration _columnConfig = columnConfig;

    private string BuildQuery((LogTableColumns Column, string Expression)[] concatExpressions) => $"SELECT {ColumnNames(concatExpressions)} FROM `{_columnConfig.TableName}`";

    private string ColumnNames((LogTableColumns Column, string Expression)[] concatExpressions) => 
        string.Join(", ", _columnConfig.ColumnMappings.Select(col => $"`{col.Value}`").Concat(concatExpressions.Select(expr => ExtractPropertyExpression(expr.Column, expr.Expression))));

    private string ExtractPropertyExpression(LogTableColumns sourceColumn, string propertyExpression) => $"`{_columnConfig.ColumnMappings[sourceColumn]}`->>'{propertyExpression}'";

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
