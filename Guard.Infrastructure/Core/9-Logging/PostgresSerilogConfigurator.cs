namespace Guard.Core.Logging;

using Serilog.Events;

public static class PostgresSerilogConfigurator
{
  public static (int BatchSizeLimit, TimeSpan Period) Calculate(
      int maxSessions,
      LogEventLevel level,
      int targetPeriodSeconds = 5)
  {
    // Коэффициент генерации событий в секунду на 1 сессию
    double kLevel = level switch
    {
      LogEventLevel.Verbose or LogEventLevel.Debug => 5.0,
      LogEventLevel.Information => 3.0,
      LogEventLevel.Warning => 0.2,
      _ => 0.02
    };

    // Формула: Сессии * Логи/сек * Таймер сброса
    int calculatedSize = (int)Math.Ceiling(maxSessions * kLevel * targetPeriodSeconds);

    // Безопасный диапазон пачки (от 10 до 10 000)
    int safeBatchSize = Math.Clamp(calculatedSize, 10, 10000);

    return (safeBatchSize, TimeSpan.FromSeconds(targetPeriodSeconds));
  }
}