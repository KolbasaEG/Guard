namespace Guard.Core.Logging;

using NpgsqlTypes;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.PostgreSQL;

public sealed class DynamicLoggerManager : IDisposable
{
  private readonly string _connectionString;
  private readonly LoggingLevelSwitch _levelSwitch;
  private readonly object gate = new();
  private readonly Func<int, LogEventLevel, Logger> create;
  private Logger? current;
  private bool disposed;
  public Logger Logger { get; }

  public DynamicLoggerManager(string connectionString, LoggingLevelSwitch levelSwitch)
  {
    _connectionString = connectionString;
    _levelSwitch = levelSwitch;
    create = (sessions, level) => BuildLogger(sessions, level);
    Logger = new LoggerConfiguration().MinimumLevel.ControlledBy(levelSwitch)
      .WriteTo.Sink(new ForwardingSink(this)).CreateLogger();
  }

  public DynamicLoggerManager(LoggingLevelSwitch levelSwitch, Func<int, LogEventLevel, Logger> create)
  {
    _connectionString = "";
    _levelSwitch = levelSwitch;
    this.create = create;
    Logger = new LoggerConfiguration().MinimumLevel.ControlledBy(levelSwitch)
      .WriteTo.Sink(new ForwardingSink(this)).CreateLogger();
  }

  public void ApplyConfiguration(int maxSessions, LogEventLevel newLevel, Action? persist = null)
  {
    if (maxSessions is < 1 or > 10000 || !Enum.IsDefined(newLevel)) throw new ArgumentException("Укажите корректный уровень и число сессий от 1 до 10000.");
    Logger? replacement = create(maxSessions, newLevel);
    try {
      lock (gate) {
        ObjectDisposedException.ThrowIf(disposed, this);
        persist?.Invoke();
        var previous = current;
        current = replacement; replacement = null;
        _levelSwitch.MinimumLevel = newLevel;
        previous?.Dispose();
      }
    }
    finally { replacement?.Dispose(); }
  }

  private Logger BuildLogger(int maxSessions, LogEventLevel level)
  {
    var (batchSize, period) = PostgresSerilogConfigurator.Calculate(maxSessions, level);

    return new LoggerConfiguration()
      .MinimumLevel.Verbose()
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

  private sealed class ForwardingSink(DynamicLoggerManager owner) : ILogEventSink
  {
    public void Emit(LogEvent logEvent) { lock (owner.gate) { if (!owner.disposed) owner.current?.Write(logEvent); } }
  }
  public void Dispose()
  {
    lock (gate) { if (disposed) return; disposed = true; current?.Dispose(); current = null; }
    Logger.Dispose();
  }

  private IDictionary<string, ColumnWriterBase> GetColumnWriters() =>
      new Dictionary<string, ColumnWriterBase>
      {
        { "message", new RenderedMessageColumnWriter() },
        { "message_template", new MessageTemplateColumnWriter() },
        { "level", new LevelColumnWriter(true, NpgsqlDbType.Varchar) }, // Принудительно строковый формат
        { "layer", new SinglePropertyColumnWriter("Layer", PropertyWriteMethod.Raw, NpgsqlDbType.Varchar) },
        { "timestamp", new UtcTimestampColumnWriter() },
        { "exception", new ExceptionColumnWriter() },
        { "properties", new LogEventSerializedColumnWriter() },
        { "user_id", new SinglePropertyColumnWriter("UserId", PropertyWriteMethod.Raw, NpgsqlDbType.Varchar) },
        { "ip_address", new SinglePropertyColumnWriter("ClientIp", PropertyWriteMethod.Raw, NpgsqlDbType.Varchar) }
      };
}
