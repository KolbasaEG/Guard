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
  private int maxSessions = 100;
  private readonly TechnicalLogRuntime? technical;
  private readonly TechnicalLogBuffer? buffer;
  public Logger Logger { get; }

  public (int MaxSessions, LogEventLevel MinimumLevel) GetConfiguration()
  {
    lock (gate) {
      ObjectDisposedException.ThrowIf(disposed, this);
      return (maxSessions, _levelSwitch.MinimumLevel);
    }
  }

  public DynamicLoggerManager(string connectionString, LoggingLevelSwitch levelSwitch, TechnicalLogRuntime? technical = null, TechnicalLogBuffer? buffer = null)
  {
    _connectionString = connectionString;
    _levelSwitch = levelSwitch;
    this.technical = technical; this.buffer = buffer;
    create = (sessions, level) => BuildLogger(sessions, level);
    var configuration = new LoggerConfiguration();
    if (technical == null) configuration.MinimumLevel.ControlledBy(levelSwitch);
    else configuration.MinimumLevel.Verbose().Filter.ByIncludingOnly(technical.Accept);
    Logger = configuration
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

  public (int MaxSessions, LogEventLevel MinimumLevel) ApplyConfiguration(int maxSessions, LogEventLevel newLevel, Action? persist = null)
  {
    if (maxSessions is < 1 or > 10000 || !Enum.IsDefined(newLevel)) throw new ArgumentException("Укажите корректный уровень и число сессий от 1 до 10000.");
    Logger? replacement = create(maxSessions, newLevel);
    try {
      lock (gate) {
        ObjectDisposedException.ThrowIf(disposed, this);
        persist?.Invoke();
        var previousConfiguration = (this.maxSessions, _levelSwitch.MinimumLevel);
        var previous = current;
        current = replacement; replacement = null;
        this.maxSessions = maxSessions;
        _levelSwitch.MinimumLevel = newLevel;
        previous?.Dispose();
        return previousConfiguration;
      }
    }
    finally { replacement?.Dispose(); }
  }

  private Logger BuildLogger(int maxSessions, LogEventLevel level)
  {
    if (technical != null && buffer != null) return new LoggerConfiguration().MinimumLevel.Verbose()
      .Enrich.FromLogContext().Enrich.With<LayerEnricher>()
      .WriteTo.Sink(new BufferForwarder(buffer)).CreateLogger();
    var (batchSize, period) = PostgresSerilogConfigurator.Calculate(maxSessions, level);

    return new LoggerConfiguration()
      .MinimumLevel.Verbose()
      // 1. Пробрасываем данные ILogger и LogContext (UserId, ClientIp)
      .Enrich.FromLogContext()
      // 2. Подключаем определение слоя (Components, Service, Database, System)
      .Enrich.With<LayerEnricher>()

      // Локальная копия всех принятых событий; это не очередь повторной отправки в БД.
      .WriteTo.File(
          formatter: new Serilog.Formatting.Json.JsonFormatter(),
          path: "logs/app-fallback-.txt",
          rollingInterval: RollingInterval.Day,
          fileSizeLimitBytes: 10 * 1024 * 1024,
          rollOnFileSizeLimit: true,
          retainedFileCountLimit: null,
          retainedFileTimeLimit: null)

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
  // Буфер принадлежит host, а не сменяемому поколению Serilog.
  private sealed class BufferForwarder(TechnicalLogBuffer target) : ILogEventSink
  { public void Emit(LogEvent entry) => target.Emit(entry); }
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
