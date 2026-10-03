using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using MySqlConnector;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using SerilogQueryApi;

namespace DemoApi;

public sealed class MySqlLogSink : ILogEventSink, IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly TableConfiguration _tableConfiguration;
    private readonly Channel<PendingLogEvent> _channel = Channel.CreateBounded<PendingLogEvent>(new BoundedChannelOptions(1024)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.DropWrite
    });

    private readonly Task _processorTask;

    public MySqlLogSink(string connectionString, TableConfiguration tableConfiguration)
    {
        _connectionString = connectionString;
        _tableConfiguration = tableConfiguration;
        _processorTask = Task.Run(ProcessAsync);
    }

    public void Emit(LogEvent logEvent)
    {
        var pending = new PendingLogEvent(
            logEvent.Timestamp.UtcDateTime,
            logEvent.Level.ToString(),
            logEvent.RenderMessage(),
            logEvent.MessageTemplate.Text,
            logEvent.Exception?.ToString(),
            JsonSerializer.Serialize(
                logEvent.Properties.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.ToString())));

        if (!_channel.Writer.TryWrite(pending))
        {
            SelfLog.WriteLine("Dropped Serilog event because the MySQL buffer is full.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await _processorTask;
    }

    private async Task ProcessAsync()
    {
        var batch = new List<PendingLogEvent>(20);

        await foreach (var logEvent in _channel.Reader.ReadAllAsync())
        {
            batch.Add(logEvent);

            if (batch.Count == 1)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }

            while (batch.Count < 20 && _channel.Reader.TryRead(out var queuedLogEvent))
            {
                batch.Add(queuedLogEvent);
            }

            await FlushBatchAsync(batch);
            batch.Clear();
        }
    }

    private async Task FlushBatchAsync(List<PendingLogEvent> batch)
    {
        if (batch.Count == 0) return;

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();

            // Build column list from table configuration
            var columnNames = new List<string>();
            var columnOrder = new List<LogTableColumns>();

            foreach (var mapping in _tableConfiguration.ColumnMappings)
            {
                columnNames.Add(mapping.Value.Name);
                columnOrder.Add(mapping.Key);
            }

            var sql = new StringBuilder($"INSERT INTO {_tableConfiguration.TableName} ({string.Join(", ", columnNames)}) VALUES");

            for (var i = 0; i < batch.Count; i++)
            {
                if (i > 0)
                {
                    sql.AppendLine(",");
                }

                var paramNames = columnOrder.Select(col => $"@{col}{i}").ToList();
                sql.Append($"({string.Join(", ", paramNames)})");

                var logEvent = batch[i];

                // Add parameters in the order defined by column mappings
                foreach (var col in columnOrder)
                {
                    var paramName = $"@{col}{i}";
                    var value = col switch
                    {
                        LogTableColumns.Timestamp => (object)logEvent.TimestampUtc,
                        LogTableColumns.Level => logEvent.Level,
                        LogTableColumns.Message => logEvent.Message,
                        LogTableColumns.MessageTemplate => logEvent.MessageTemplate,
                        LogTableColumns.PropertiesJson => logEvent.PropertiesJson,
                        _ => throw new InvalidOperationException($"Unknown column type: {col}")
                    };

                    command.Parameters.AddWithValue(paramName, value ?? DBNull.Value);
                }
            }

            command.CommandText = sql.ToString();
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            SelfLog.WriteLine("Failed to write Serilog batch to MySQL: {0}", ex);
        }
    }

    private sealed record PendingLogEvent(
        DateTime TimestampUtc,
        string Level,
        string Message,
        string MessageTemplate,
        string? Exception,
        string PropertiesJson);
}
