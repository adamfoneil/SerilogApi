namespace SerilogQueryApi;

public enum LogTableColumns
{
    Timestamp,
    Level,    
    MessageTemplate,
    Message,
    PropertiesJson
}

public class ColumnConfiguration(
    string serilogTableName,
    IDictionary<LogTableColumns, string> columnMappings)
{
    public string TableName { get; } = serilogTableName;

    public Dictionary<LogTableColumns, string> ColumnMappings { get; } = columnMappings.ToDictionary();

    public static ColumnConfiguration MySqlDefault => new(
        "serilog",
        new Dictionary<LogTableColumns, string>
        {
            [LogTableColumns.Timestamp] = "_ts",
            [LogTableColumns.Level] = "Level",
            [LogTableColumns.MessageTemplate] = "MessageTemplate",
            [LogTableColumns.Message] = "Message",
            [LogTableColumns.PropertiesJson] = "Properties"
        });
}
