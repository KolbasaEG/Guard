namespace Guard.Core.Logging;

using NpgsqlTypes;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.PostgreSQL;

public class DynamicLoggerManager
{
  private readonly string _connectionString;
  private readonly LoggingLevelSwitch _levelSwitch;

  public DynamicLoggerManager(string connectionString, LoggingLevelSwitch levelSwitch)
  {
    _connectionString = connectionString;
    _levelSwitch = levelSwitch;
  }

  public void ApplyConfiguration(int maxSessions, LogEventLevel newLevel)
  {
    _levelSwitch.MinimumLevel = newLevel;
    var (batchSize, period) = PostgresSerilogConfigurator.Calculate(maxSessions, newLevel);

    Log.Logger = new LoggerConfiguration()
      .MinimumLevel.ControlledBy(_levelSwitch)
      // 1. Пробрасываем данные ILogger и LogContext (UserId, ClientIp)
      .Enrich.FromLogContext()
      // 2. Подключаем определение слоя (Components, Service, Database, System)
      .Enrich.With<LayerEnricher>()

      // 3. ФАЛЛБЭК: Локальный файл
      .WriteTo.File(
          path: "logs/app-fallback-.txt",
          rollingInterval: RollingInterval.Day,
          retainedFileCountLimit: 7,
          restrictedToMinimumLevel: LogEventLevel.Warning)

      // 4. ОСНОВНОЙ SINK: PostgreSQL
      .WriteTo.PostgreSQL(
          connectionString: _connectionString,
          tableName: "logs",
          columnOptions: GetColumnWriters(),
          batchSizeLimit: batchSize,
          period: period,
          needAutoCreateTable: false)
      .CreateLogger();
  }

  private IDictionary<string, ColumnWriterBase> GetColumnWriters() =>
      new Dictionary<string, ColumnWriterBase>
      {
        { "message", new RenderedMessageColumnWriter() },
        { "message_template", new MessageTemplateColumnWriter() },
        { "level", new LevelColumnWriter(true, NpgsqlDbType.Varchar) }, // Принудительно строковый формат
        { "layer", new SinglePropertyColumnWriter("Layer", PropertyWriteMethod.Raw, NpgsqlDbType.Varchar) },
        { "timestamp", new TimestampColumnWriter() },
        { "exception", new ExceptionColumnWriter() },
        { "properties", new LogEventSerializedColumnWriter() },
        { "user_id", new SinglePropertyColumnWriter("UserId", PropertyWriteMethod.Raw, NpgsqlDbType.Varchar) },
        { "ip_address", new SinglePropertyColumnWriter("ClientIp", PropertyWriteMethod.Raw, NpgsqlDbType.Varchar) }
      };
}