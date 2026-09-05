using System.Text.Json;
using MySqlConnector;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;

namespace DemoApi;

public sealed class MySqlLogSink(string connectionString) : ILogEventSink
{
    private readonly string _connectionString = connectionString;

    public void Emit(LogEvent logEvent)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO serilog_events
                    (TimestampUtc, Level, Message, MessageTemplate, Exception, PropertiesJson)
                VALUES
                    (@timestampUtc, @level, @message, @messageTemplate, @exception, @propertiesJson);
                """;

            command.Parameters.AddWithValue("@timestampUtc", logEvent.Timestamp.UtcDateTime);
            command.Parameters.AddWithValue("@level", logEvent.Level.ToString());
            command.Parameters.AddWithValue("@message", logEvent.RenderMessage());
            command.Parameters.AddWithValue("@messageTemplate", logEvent.MessageTemplate.Text);
            command.Parameters.AddWithValue("@exception", logEvent.Exception?.ToString());
            command.Parameters.AddWithValue("@propertiesJson", JsonSerializer.Serialize(
                logEvent.Properties.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.ToString())));

            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            SelfLog.WriteLine("Failed to write Serilog event to MySQL: {0}", ex);
        }
    }
}
