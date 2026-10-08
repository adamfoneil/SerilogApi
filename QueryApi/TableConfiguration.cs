namespace SerilogQueryApi;

public enum LogTableColumns
{
    Timestamp,
    Level,    
    MessageTemplate,
    Message,
    PropertiesJson,
    Exception
}

/// <summary>
/// Represents a column in the Serilog events table.
/// </summary>
public record Column(
    // name of the column in the database table
    string Name,
    // if specified, match to LogEntry property name, e.g. "PropertiesJson" for the Properties column
    string? Alias = null,
    // property name on the entity (e.g., "TimestampUtc", "Level", "Message", etc.)
    // defaults to the Alias or the column type name if not specified
    string? PropertyName = null,
    // optional max length constraint for string columns
    int? MaxLength = null,
    // optional column type override (e.g., "longtext")
    string? ColumnType = null)
{
    /// <summary>
    /// Gets the property name to use for EF Core mapping.
    /// Defaults to Alias if provided, otherwise to the LogTableColumns enum name.
    /// </summary>
    public string GetPropertyName(LogTableColumns columnType) =>
        PropertyName ?? Alias ?? columnType.ToString();
}

/// <summary>
/// describes a serilog logging database table along with optional delegate for describing how to materialize query results to the LogEntry DTO type
/// </summary>
public class TableConfiguration(
    string tableName,
    Dictionary<LogTableColumns, Column> columnMappings,
    Func<dynamic, LogEntry>? logEntryMaterializer = null)
{
    public string TableName { get; } = tableName;

    public Dictionary<LogTableColumns, Column> ColumnMappings { get; } = columnMappings.ToDictionary();

    /// <summary>
    /// Factory function to convert dynamic query results into LogEntry instances.
    /// Maps column aliases/names from the result set to the LogEntry constructor.
    /// Accepts dynamic (including Dapper.DapperRow) which implements dictionary-like access.
    /// </summary>
    public Func<dynamic, LogEntry> LogEntryMaterializer { get; } = 
        logEntryMaterializer ?? DefaultLogEntryMaterializer;

    /// <summary>
    /// Default materializer that constructs LogEntry from dynamic result columns.
    /// Assumes standard column naming/aliasing conventions.
    /// </summary>
    private static LogEntry DefaultLogEntryMaterializer(dynamic row)
    {
        var timestamp = row.Timestamp;
        DateTime parsedTimestamp = timestamp switch
        {
            DateTime dt => dt,
            string s => DateTime.TryParse(s, out var parsed) ? parsed : DateTime.MinValue,
            _ => DateTime.MinValue
        };

        return new LogEntry(
            Timestamp: parsedTimestamp,
            Level: row.Level?.ToString() ?? string.Empty,
            MessageTemplate: row.MessageTemplate?.ToString() ?? string.Empty,
            Message: row.Message?.ToString() ?? string.Empty,
            PropertiesJson: row.PropertiesJson?.ToString() ?? string.Empty,
            Exception: row.Exception?.ToString()
        );
    }
}
