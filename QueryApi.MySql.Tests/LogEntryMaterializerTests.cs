using SerilogQueryApi;
using Xunit;

namespace QueryApi.MySql.Tests;

public class LogEntryMaterializerTests
{
    [Fact]
    public void DefaultLogEntryMaterializer_CreatesLogEntryFromDictionary()
    {
        // Arrange
        var tableConfig = MySqlLogQuery.DefaultTableConfiguration;
        var rowData = new Dictionary<string, object?>
        {
            { "Timestamp", DateTime.Parse("2026-01-15T10:30:00Z") },
            { "Level", "Information" },
            { "MessageTemplate", "User {UserId} logged in" },
            { "Message", "User 42 logged in" },
            { "PropertiesJson", @"{""UserId"":42}" }
        };

        // Act
        var logEntry = tableConfig.LogEntryMaterializer(rowData);

        // Assert
        Assert.NotNull(logEntry);
        Assert.Equal(DateTime.Parse("2026-01-15T10:30:00Z"), logEntry.Timestamp);
        Assert.Equal("Information", logEntry.Level);
        Assert.Equal("User {UserId} logged in", logEntry.MessageTemplate);
        Assert.Equal("User 42 logged in", logEntry.Message);
        Assert.Equal(@"{""UserId"":42}", logEntry.PropertiesJson);
    }

    [Fact]
    public void DefaultLogEntryMaterializer_HandlesNullValues()
    {
        // Arrange
        var tableConfig = MySqlLogQuery.DefaultTableConfiguration;
        var rowData = new Dictionary<string, object?>
        {
            { "Timestamp", null },
            { "Level", null },
            { "MessageTemplate", null },
            { "Message", null },
            { "PropertiesJson", null }
        };

        // Act
        var logEntry = tableConfig.LogEntryMaterializer(rowData);

        // Assert
        Assert.NotNull(logEntry);
        Assert.Equal(DateTime.MinValue, logEntry.Timestamp);
        Assert.Equal(string.Empty, logEntry.Level);
        Assert.Equal(string.Empty, logEntry.MessageTemplate);
        Assert.Equal(string.Empty, logEntry.Message);
        Assert.Equal(string.Empty, logEntry.PropertiesJson);
    }

    [Fact]
    public void DefaultLogEntryMaterializer_HandlesMissingKeys()
    {
        // Arrange
        var tableConfig = MySqlLogQuery.DefaultTableConfiguration;
        var rowData = new Dictionary<string, object?>(); // Empty dict

        // Act
        var logEntry = tableConfig.LogEntryMaterializer(rowData);

        // Assert
        Assert.NotNull(logEntry);
        Assert.Equal(DateTime.MinValue, logEntry.Timestamp);
        Assert.Equal(string.Empty, logEntry.Level);
    }
}
