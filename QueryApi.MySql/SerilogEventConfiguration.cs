using SerilogQueryApi;

namespace QueryApi.MySql;

public static class SerilogEventConfiguration
{
    public static string CreateIfNotExistsSql(TableConfiguration tableConfig) =>
        GenerateCreateTableSql(tableConfig);

    private static string GenerateCreateTableSql(TableConfiguration tableConfig)
    {
        var columns = new List<string>
        {
            "`Id` bigint NOT NULL AUTO_INCREMENT"
        };

        foreach (var (columnType, column) in tableConfig.ColumnMappings)
        {
            columns.Add(GenerateColumnDefinition(column));
        }

        columns.Add("PRIMARY KEY (`Id`)");

        var columnDefinitions = string.Join(",\r\n            ", columns);

        return $"""
            CREATE TABLE IF NOT EXISTS `{tableConfig.TableName}` (
                {columnDefinitions}
            ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
            """;
    }

    private static string GenerateColumnDefinition(Column column)
    {
        var def = $"`{column.Name}`";

        // Determine the column type
        string columnType;
        if (!string.IsNullOrWhiteSpace(column.ColumnType))
        {
            columnType = column.ColumnType;
        }
        else if (column.MaxLength.HasValue)
        {
            columnType = $"varchar({column.MaxLength.Value})";
        }
        else
        {
            columnType = "varchar(255)";
        }

        def += $" {columnType}";

        // All columns are nullable by default in Serilog schema
        def += " NULL";

        return def;
    }
}
