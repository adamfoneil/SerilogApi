using Dapper;
using MySqlConnector;
using SerilogQueryApi;
using System.Text.Json;

namespace QueryApi.MySql;

public partial class MySqlLogQuery(string connectionString, ColumnConfiguration columnConfig) : ILogQuery
{
    private readonly string _connectionString = connectionString;
    private readonly ColumnConfiguration _columnConfig = columnConfig;

    private (string Sql, DynamicParameters Parameters) BuildQuery(JsonColumn[] concatExpressions, LogCriteria criteria, SortOptions sortOptions, int limit) => 
        (@$"SELECT {ColumnNames(concatExpressions)} 
        FROM `{_columnConfig.TableName}` 
        {WhereClause(criteria, out var parameters)} 
        ORDER BY {SortColumn(sortOptions)} 
        LIMIT {limit}", parameters);

    private string ColumnNames(JsonColumn[] concatExpressions) => 
        string.Join(", ", 
            _columnConfig.ColumnMappings.Select(col => $"`{col.Value}`")
            .Concat(concatExpressions.Select(expr => ExtractPropertyExpression(expr))));

    private string SortColumn(SortOptions sortOptions) => sortOptions switch
    {
        SortOptions.TimestampAsc => $"`{_columnConfig.ColumnMappings[LogTableColumns.Timestamp]}` ASC",
        SortOptions.TimestampDesc => $"`{_columnConfig.ColumnMappings[LogTableColumns.Timestamp]}` DESC",
        _ => throw new ArgumentOutOfRangeException(nameof(sortOptions), sortOptions, null)
    };

    private string WhereClause(LogCriteria criteria, out DynamicParameters parameters)
    {
        var terms = new List<string>();
        parameters = new DynamicParameters();
        
        if (ParseDateExpression(criteria.DateTimeExpression, parameters, out var expr))
        {
            terms.Add(expr);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Level))
        {
            terms.Add($"`{_columnConfig.ColumnMappings[LogTableColumns.Level]}` = @Level");
            parameters.Add("Level", criteria.Level);
        }

        if (!string.IsNullOrWhiteSpace(criteria.MessageContains))
        {
            terms.Add($"`{_columnConfig.ColumnMappings[LogTableColumns.Message]}` LIKE @MessageContains");
            parameters.Add("MessageContains", $"%{criteria.MessageContains}%");
        }

        if (!string.IsNullOrWhiteSpace(criteria.MessageExcludes))
        {
            terms.Add($"`{_columnConfig.ColumnMappings[LogTableColumns.Message]}` NOT LIKE @MessageExcludes");
            parameters.Add("MessageExcludes", $"%{criteria.MessageExcludes}%");
        }

        if (criteria.MessageProperties?.Count > 0)
        {
            foreach (var kvp in criteria.MessageProperties)
            {
                terms.Add($"JSON_EXTRACT(`{_columnConfig.ColumnMappings[LogTableColumns.Message]}`, '$.{kvp.Key}') = @{kvp.Key}");
                parameters.Add(kvp.Key, kvp.Value);
            }
        }

        if (criteria.Properties?.Count > 0)
        {
            foreach (var kvp in criteria.Properties)
            {
                terms.Add($"JSON_EXTRACT(`{_columnConfig.ColumnMappings[LogTableColumns.PropertiesJson]}`, '$.{kvp.Key}') = @{kvp.Key}");
                parameters.Add(kvp.Key, kvp.Value);
            }
        }

        return terms.Count > 0 ? " WHERE " + string.Join(" AND ", terms) : string.Empty;
    }

    private static bool ParseDateExpression(string? dateTimeExpression, DynamicParameters parameters, out string expr)
    {
        expr = string.Empty;
        if (string.IsNullOrWhiteSpace(dateTimeExpression))
        {
            return false;
        }

        string input = dateTimeExpression!.Trim();
        // Helper: parse a single date token which can be:
        // - ISO/parseable date/time
        // - "now" optionally with +/- offsets like now-1h30m or now+2d
        // - "today" or "yesterday"
        DateTimeOffset? ParseToken(string token)
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
                    switch (unit)
                    {
                        case "y": baseTime = baseTime.AddYears(signed); break;
                        case "M": baseTime = baseTime.AddMonths(signed); break;
                        case "d": baseTime = baseTime.AddDays(signed); break;
                        case "h": baseTime = baseTime.AddHours(signed); break;
                        case "m": baseTime = baseTime.AddMinutes(signed); break;
                        case "s": baseTime = baseTime.AddSeconds(signed); break;
                        default: return null;
                    }
                }
                return baseTime;
            }

            // Try to parse as a date/time (prefer invariant/UTC)
            if (DateTimeOffset.TryParse(token, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var dto))
                return dto;

            if (DateTime.TryParse(token, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var dt))
                return new DateTimeOffset(dt.ToUniversalTime());

            return null;
        }

        // Helper: unique parameter name
        string GetUniqueParamName(string baseName)
        {
            int i = 0;
            string name;
            var existing = new HashSet<string>(parameters.ParameterNames ?? [], StringComparer.OrdinalIgnoreCase);
            do
            {
                name = $"{baseName}{i}";
                i++;
            } while (existing.Contains(name));
            return name;
        }

        // Support range separators ".." or "/" or ":" (common choices)
        string[] rangeSeparators = ["..", "/", ":"];
        foreach (var sep in rangeSeparators)
        {
            if (input.Contains(sep))
            {
                var parts = input.Split([sep], StringSplitOptions.None);
                if (parts.Length == 2)
                {
                    var left = ParseToken(parts[0]);
                    var right = ParseToken(parts[1]);

                    if (left == null && right == null)
                        return false;

                    // Add parameters and build expression
                    if (left != null && right != null)
                    {
                        var p1 = GetUniqueParamName("date");
                        var p2 = GetUniqueParamName("date");
                        parameters.Add(p1, left.Value.UtcDateTime, System.Data.DbType.DateTime);
                        parameters.Add(p2, right.Value.UtcDateTime, System.Data.DbType.DateTime);
                        expr = $"Timestamp BETWEEN @{p1} AND @{p2}";
                        return true;
                    }
                    if (left != null)
                    {
                        var p = GetUniqueParamName("date");
                        parameters.Add(p, left.Value.UtcDateTime, System.Data.DbType.DateTime);
                        expr = $"Timestamp >= @{p}";
                        return true;
                    }
                    // right != null
                    var pr = GetUniqueParamName("date");
                    parameters.Add(pr, right.Value.UtcDateTime, System.Data.DbType.DateTime);
                    expr = $"Timestamp <= @{pr}";
                    return true;
                }
                // Not a valid range
                return false;
            }
        }

        // Operators: >=, <=, >, <, =
        var opMatch = MyRegex().Match(input);
        if (opMatch.Success)
        {
            var op = opMatch.Groups[1].Value;
            var token = opMatch.Groups[2].Value;
            var dt = ParseToken(token);
            if (dt == null)
                return false;
            var pname = GetUniqueParamName("date");
            parameters.Add(pname, dt.Value.UtcDateTime, System.Data.DbType.DateTime);
            expr = $"Timestamp {op} @{pname}";
            return true;
        }

        // No operator: treat as equality or single bound >=
        // If token contains space, maybe user provided a time range like "2024-01-01 12:00"
        var single = ParseToken(input);
        if (single != null)
        {
            var pname = GetUniqueParamName("date");
            parameters.Add(pname, single.Value.UtcDateTime, System.Data.DbType.DateTime);
            expr = $"Timestamp >= @{pname}";
            return true;
        }

        return false;
    }

    private string ExtractPropertyExpression(JsonColumn jsonColumn) => $"`{_columnConfig.ColumnMappings[jsonColumn.Column]}`->>'{jsonColumn.Expression}' AS `{jsonColumn.Alias}`";

    public Task<LogEntry[]> QueryAsync(LogCriteria filter, JsonColumn[] concatColumns, SortOptions sortOptions = SortOptions.TimestampDesc)
    {
        throw new NotImplementedException();
    }

    private static JsonColumn SourceContext => new(LogTableColumns.PropertiesJson, "SourceContext", "$.SourceContext");
    private static JsonColumn RequestId => new(LogTableColumns.PropertiesJson, "RequestId", "$.RequestId");

    public async Task<ErrorInfo[]> RecentErrorsAsync(string? dateTimeExpression = null)
    {
        var (sql, parameters) = BuildQuery([SourceContext, RequestId], new()
        {
            DateTimeExpression = dateTimeExpression,
            Level = "Error"
        }, SortOptions.TimestampDesc, 200);

        var logEntries = await QueryInternalAsync(sql, parameters);

        return [..logEntries
            .GroupBy(row => (row.SourceContext, row.MessageTemplate)).Select(grp => 
                new ErrorInfo(
                    grp.First().Age, grp.Key.SourceContext, grp.Key.MessageTemplate, 
                    [..grp.Select(row => row.Properties.GetValueOrDefault("RequestId", "<not set>"))]
                    ))];
    }

    private async Task<IEnumerable<LogEntry>> QueryInternalAsync(string sql, DynamicParameters parameters)
    {
        var cn = new MySqlConnection(_connectionString);

        var logEntries = await cn.QueryAsync<LogEntry>(sql, parameters);

        var utcNow = DateTime.UtcNow;
        foreach (var row in logEntries)
        {
            row.Age = utcNow - row.Timestamp;
            row.Properties = JsonSerializer.Deserialize<Dictionary<string, string>>(row.JsonData) ?? [];
        }

        return logEntries;
    }

    public Task<LogEntry[]> TraceAsync(string requestId, JsonColumn[] concatColumns)
    {
        var sql = BuildQuery(concatColumns, new()
        {
            Properties = new()
            {
                ["RequestId"] = requestId
            }
        }, SortOptions.TimestampAsc, 20);

        throw new NotImplementedException();
    }

    private static System.Text.RegularExpressions.Regex MyRegex() =>
        new(@"^(<=|>=|<|>|=)\s*(.+)$", System.Text.RegularExpressions.RegexOptions.Compiled);
}
