using Dapper;
using MySqlConnector;
using SerilogQueryApi;
using System.Text.Json;

namespace QueryApi.MySql;

public partial class MySqlLogQuery(string connectionString, TableConfiguration columnConfig) : ILogQuery
{
    private readonly string _connectionString = connectionString;
    private readonly TableConfiguration _columnConfig = columnConfig;

    private (string Sql, DynamicParameters Parameters) BuildQuery(JsonColumn[] concatExpressions, LogCriteria? criteria, SortOptions sortOptions, int skip, int take) => 
        (@$"SELECT {ColumnNames(concatExpressions)} 
        FROM `{_columnConfig.TableName}` 
        {WhereClause(criteria, out var parameters)} 
        ORDER BY {SortColumn(sortOptions)} 
        LIMIT {take} OFFSET {skip}", parameters);

    private string ColumnNames(JsonColumn[] concatExpressions) => 
        string.Join(", ", 
            _columnConfig.ColumnMappings.Select(col =>
            {
                var result = $"`{col.Value.Name}`";
                if (!string.IsNullOrWhiteSpace(col.Value.Alias))
                {
                    result += $" AS `{col.Value.Alias}`";
                }
                return result;
            })
            .Concat(concatExpressions.Select(expr => ExtractPropertyExpression(expr))));

    private string SortColumn(SortOptions sortOptions) => sortOptions switch
    {
        SortOptions.TimestampAsc => $"`{_columnConfig.ColumnMappings[LogTableColumns.Timestamp].Name}` ASC",
        SortOptions.TimestampDesc => $"`{_columnConfig.ColumnMappings[LogTableColumns.Timestamp].Name}` DESC",
        _ => throw new ArgumentOutOfRangeException(nameof(sortOptions), sortOptions, null)
    };

    private string WhereClause(LogCriteria? criteria, out DynamicParameters parameters)
    {
        parameters = new DynamicParameters();

        if (criteria is null) return string.Empty;

        var terms = new List<string>();        

        if (ParseDateExpression(criteria.DateTimeExpression, parameters, out var expr))
        {
            terms.Add(expr);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Level))
        {
            terms.Add($"`{_columnConfig.ColumnMappings[LogTableColumns.Level].Name}` = @Level");
            parameters.Add("Level", criteria.Level);
        }

        if (!string.IsNullOrWhiteSpace(criteria.MessageContains))
        {
            terms.Add($"`{_columnConfig.ColumnMappings[LogTableColumns.Message].Name}` LIKE @MessageContains");
            parameters.Add("MessageContains", $"%{criteria.MessageContains}%");
        }

        if (!string.IsNullOrWhiteSpace(criteria.MessageExcludes))
        {
            terms.Add($"`{_columnConfig.ColumnMappings[LogTableColumns.Message].Name}` NOT LIKE @MessageExcludes");
            parameters.Add("MessageExcludes", $"%{criteria.MessageExcludes}%");
        }

        if (criteria.MessageProperties?.Count > 0)
        {
            foreach (var kvp in criteria.MessageProperties)
            {
                terms.Add($"JSON_EXTRACT(`{_columnConfig.ColumnMappings[LogTableColumns.Message].Name}`, '$.{kvp.Key}') = @{kvp.Key}");
                parameters.Add(kvp.Key, kvp.Value);
            }
        }

        if (criteria.Properties?.Count > 0)
        {
            foreach (var kvp in criteria.Properties)
            {
                terms.Add($"JSON_EXTRACT(`{_columnConfig.ColumnMappings[LogTableColumns.PropertiesJson].Name}`, '$.{kvp.Key}') = @{kvp.Key}");
                parameters.Add(kvp.Key, kvp.Value);
            }
        }

        return terms.Count > 0 ? " WHERE " + string.Join(" AND ", terms) : string.Empty;
    }

    private bool ParseDateExpression(string? dateTimeExpression, DynamicParameters parameters, out string expr)
    {
        expr = string.Empty;
        if (string.IsNullOrWhiteSpace(dateTimeExpression))
        {
            return false;
        }

        string input = dateTimeExpression!.Trim();
        var timestampColumn = $"`{_columnConfig.ColumnMappings[LogTableColumns.Timestamp].Name}`";

        // Helper: parse a single date token which can be:
        // - ISO/parseable date/time
        // - "now" optionally with +/- offsets like now-1h30m or now+2d
        // - "today" or "yesterday"
        static DateTimeOffset? ParseToken(string token)
        {
            token = token.Trim();
            if (string.IsNullOrEmpty(token))
                return null;

            // today / yesterday
            if (string.Equals(token, "today", StringComparison.OrdinalIgnoreCase))
            {
                var today = DateTime.UtcNow.Date;
                return new DateTimeOffset(today, TimeSpan.Zero);
            }
            if (string.Equals(token, "yesterday", StringComparison.OrdinalIgnoreCase))
            {
                var day = DateTime.UtcNow.Date.AddDays(-1);
                return new DateTimeOffset(day, TimeSpan.Zero);
            }

            // now with offsets, support sequences like now-1h30m+2s
            if (token.StartsWith("now", StringComparison.OrdinalIgnoreCase))
            {
                var baseTime = DateTimeOffset.UtcNow;
                string rest = token.Substring(3);
                if (string.IsNullOrEmpty(rest))
                    return baseTime;

                // parse sequences of (+|-)<number><unit> where unit is y,M,d,h,m,s
                var rx = new System.Text.RegularExpressions.Regex(@"([+-])\s*(\d+)\s*([yMdhms])", System.Text.RegularExpressions.RegexOptions.Compiled);
                var matches = rx.Matches(rest);
                if (matches.Count == 0)
                    return null;

                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    var sign = m.Groups[1].Value == "-" ? -1 : 1;
                    if (!int.TryParse(m.Groups[2].Value, out int val))
                        return null;
                    var unit = m.Groups[3].Value;
                    int signed = sign * val;

                    DateTimeOffset? newTime = unit switch
                    {
                        "y" => baseTime.AddYears(signed),
                        "M" => baseTime.AddMonths(signed),
                        "d" => baseTime.AddDays(signed),
                        "h" => baseTime.AddHours(signed),
                        "m" => baseTime.AddMinutes(signed),
                        "s" => baseTime.AddSeconds(signed),
                        _ => null
                    };

                    if (newTime == null)
                        return null;

                    baseTime = newTime.Value;
                }
                return baseTime;
            }

            // Try parse ISO / general date parsing
            if (DateTimeOffset.TryParse(token, out var parsed))
                return parsed;

            return null;
        }

        // Range: token..token
        if (input.Contains(".."))
        {
            var parts = input.Split(new[] { ".." }, StringSplitOptions.None);
            if (parts.Length != 2)
                return false;

            var from = ParseToken(parts[0]);
            var to = ParseToken(parts[1]);
            if (from == null || to == null)
                return false;

            parameters.Add("From", from.Value.UtcDateTime);
            parameters.Add("To", to.Value.UtcDateTime);
            expr = $"{timestampColumn} BETWEEN @From AND @To";
            return true;
        }

        // Comparison operators: <, >, <=, >=
        {
            var m = new System.Text.RegularExpressions.Regex(@"^(<=|>=|<|>)(.+)$", System.Text.RegularExpressions.RegexOptions.Compiled).Match(input);
            if (m.Success)
            {
                var op = m.Groups[1].Value;
                var token = m.Groups[2].Value;
                var dt = ParseToken(token);
                if (dt == null)
                    return false;
                parameters.Add("Bound", dt.Value.UtcDateTime);
                expr = $"{timestampColumn} {op} @Bound";
                return true;
            }
        }

        // Exact match (fallback)
        var single = ParseToken(input);
        if (single == null)
            return false;
        parameters.Add("Exact", single.Value.UtcDateTime);
        expr = $"{timestampColumn} = @Exact";
        return true;
    }

    private string ExtractPropertyExpression(JsonColumn jsonColumn) => $"`{_columnConfig.ColumnMappings[jsonColumn.Column]}`->>'{jsonColumn.Expression}' AS `{jsonColumn.Alias}`";

    public async Task<LogEntry[]> QueryAsync(LogCriteria filter, JsonColumn[]? concatColumns = null, SortOptions sortOptions = SortOptions.TimestampDesc)
    {
        var (sql, parameters) = BuildQuery(concatColumns ?? [], filter, sortOptions, filter?.Skip ?? 0, filter?.Take > 0 ? filter.Take : 100);
        var results = await QueryInternalAsync(sql, parameters);
        return [.. results];
    }

    private static JsonColumn SourceContext => new(LogTableColumns.PropertiesJson, "SourceContext", "$.SourceContext");
    private static JsonColumn RequestId => new(LogTableColumns.PropertiesJson, "RequestId", "$.RequestId");

    public async Task<ErrorInfo[]> RecentErrorsAsync(string? dateTimeExpression = null)
    {
        var (sql, parameters) = BuildQuery([], new()
        {
            DateTimeExpression = dateTimeExpression,
            Level = "Error"
        }, SortOptions.TimestampDesc, 0, 200);

        var logEntries = await QueryInternalAsync(sql, parameters);

        return [..logEntries
            .GroupBy(row => (row.SourceContext, row.MessageTemplate)).Select(grp => 
                new ErrorInfo(
                    grp.First().Age, grp.Key.SourceContext ?? string.Empty, grp.Key.MessageTemplate, 
                    [..grp.Select(row => row.RequestId ?? "<not set>")]
                    ))];
    }

    private async Task<IEnumerable<LogEntry>> QueryInternalAsync(string sql, DynamicParameters parameters)
    {
        var cn = new MySqlConnection(_connectionString);

        // Query as dynamic to avoid Dapper's strict type mapping, then materialize using configured converter
        var dynamicResults = await cn.QueryAsync(sql, parameters);
        var logEntries = dynamicResults
            .Select(row => _columnConfig.LogEntryMaterializer(row))
            .Cast<LogEntry>()
            .ToList();

        var utcNow = DateTime.UtcNow;
        foreach (var row in logEntries)
        {
            var propsResult = JsonSerializer.Deserialize<Dictionary<string, object>>(row.PropertiesJson);
            var props = propsResult ?? [];
            row.Age = utcNow - row.Timestamp;
            row.Properties = props;
            row.SourceContext = props.GetValueOrDefault("SourceContext")?.ToString();
            row.RequestId = props.GetValueOrDefault("RequestId")?.ToString();
        }

        return logEntries;
    }

    public async Task<LogEntry[]> TraceAsync(string requestId, JsonColumn[]? concatColumns = null)
    {
        var (sql, parameters) = BuildQuery(concatColumns ?? [], new()
        {
            Properties = new()
            {
                ["RequestId"] = requestId
            }
        }, SortOptions.TimestampAsc, 0, 20);

        return [.. await QueryInternalAsync(sql, parameters)];
    }

    private static System.Text.RegularExpressions.Regex MyRegex() =>
        new(@"^(<=|>=|<|>|=)\s*(.+)$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public static TableConfiguration DefaultTableConfiguration => new(
        "serilog_events",
        new Dictionary<LogTableColumns, Column>
        {
            [LogTableColumns.Timestamp] = new("_ts", "Timestamp", "TimestampUtc"),
            [LogTableColumns.Level] = new("Level", null, "Level", MaxLength: 32),
            [LogTableColumns.MessageTemplate] = new("MessageTemplate", null, "MessageTemplate", MaxLength: 4000),
            [LogTableColumns.Message] = new("Message", null, "Message", MaxLength: 4000),
            [LogTableColumns.PropertiesJson] = new("Properties", "PropertiesJson", "PropertiesJson", ColumnType: "json")
        });
}
