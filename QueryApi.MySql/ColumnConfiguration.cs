namespace QueryApi.MySql;

public enum LogTableColumns
{
    Timestamp,
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
}
