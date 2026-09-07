namespace SerilogQueryApi;

public enum LogTableColumns
{
    Timestamp,
    Level,    
    MessageTemplate,
    Message,
    PropertiesJson
}

public record Column(
    // name of the column in the database table
    string Name,
    // if specified, match to LogEntry property name, e.g. "PropertiesJson" for the Properties column
    string? Alias = null);

public class ColumnConfiguration(
    string serilogTableName,
    IDictionary<LogTableColumns, Column> columnMappings)
{
    public string TableName { get; } = serilogTableName;

    public Dictionary<LogTableColumns, Column> ColumnMappings { get; } = columnMappings.ToDictionary();

    public static ColumnConfiguration MySqlDefault => new(
        "serilog",
        new Dictionary<LogTableColumns, Column>
        {
            [LogTableColumns.Timestamp] = new("_ts", "Timestamp"),
            [LogTableColumns.Level] = new("Level"),
            [LogTableColumns.MessageTemplate] = new("MessageTemplate"),
            [LogTableColumns.Message] = new("Message"),
            [LogTableColumns.PropertiesJson] = new("Properties", "PropertiesJson")
        });
}
