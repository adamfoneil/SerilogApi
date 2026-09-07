using SerilogQueryApi;

namespace QueryApi.MySql;

public class MySqlLogQuery(string connectionString, ColumnConfiguration columnConfig) : ILogQuery
{
    private readonly string _connectionString = connectionString;
    private readonly ColumnConfiguration _columnConfig = columnConfig;

    private string BuildQuery(JsonColumn[] concatExpressions, LogCriteria criteria) => 
        $"SELECT {ColumnNames(concatExpressions)} FROM `{_columnConfig.TableName}` {WhereClause(criteria)}";

    private string ColumnNames(JsonColumn[] concatExpressions) => 
        string.Join(", ", 
            _columnConfig.ColumnMappings.Select(col => $"`{col.Value}`")
            .Concat(concatExpressions.Select(expr => ExtractPropertyExpression(expr))));

    private string WhereClause(LogCriteria criteria)
    {
        var terms = new List<string>();
        
        if (ParseDateExpression(criteria.DateTimeExpression, out var expr))
        {
            terms.Add(expr);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Level))
        {
            terms.Add($"`{_columnConfig.ColumnMappings[LogTableColumns.Level]}` = '{criteria.Level}'");
        }

        if (!string.IsNullOrWhiteSpace(criteria.MessageContains))
        {
            terms.Add($"`{_columnConfig.ColumnMappings[LogTableColumns.Message]}` LIKE '%{criteria.MessageContains}%'");
        }

        if (!string.IsNullOrWhiteSpace(criteria.MessageExcludes))
        {
            terms.Add($"`{_columnConfig.ColumnMappings[LogTableColumns.Message]}` NOT LIKE '%{criteria.MessageExcludes}%'");
        }

        if (criteria.MessageProperties?.Count > 0)
        {
            foreach (var kvp in criteria.MessageProperties)
            {
                terms.Add($"JSON_EXTRACT(`{_columnConfig.ColumnMappings[LogTableColumns.Message]}`, '$.{kvp.Key}') = '{kvp.Value}'");
            }
        }

        if (criteria.Properties?.Count > 0)
        {
            foreach (var kvp in criteria.Properties)
            {
                terms.Add($"JSON_EXTRACT(`{_columnConfig.ColumnMappings[LogTableColumns.PropertiesJson]}`, '$.{kvp.Key}') = '{kvp.Value}'");
            }
        }

        return terms.Count > 0 ? " WHERE " + string.Join(" AND ", terms) : string.Empty;
    }

    private static bool ParseDateExpression(string? dateTimeExpression, out string expr)
    {
        if (string.IsNullOrWhiteSpace(dateTimeExpression))
        {
            expr = string.Empty;
            return false;
        }

        throw new NotImplementedException();
    }

    private string ExtractPropertyExpression(JsonColumn jsonColumn) => $"`{_columnConfig.ColumnMappings[jsonColumn.Column]}`->>'{jsonColumn.Expression}' AS `{jsonColumn.Alias}`";

    public Task<LogEntry[]> QueryAsync(LogCriteria filter, JsonColumn[] concatColumns)
    {
        throw new NotImplementedException();
    }

    public Task<ErrorInfo[]> RecentErrorsAsync(string? dateTimeExpression = null)
    {
        throw new NotImplementedException();
    }

    public Task<LogEntry[]> TraceAsync(string requestId, JsonColumn[] concatColumns)
    {
        var sql = BuildQuery(concatColumns, new()
        {
            Properties = new()
            {
                ["RequestId"] = requestId
            }
        });
    }
}
