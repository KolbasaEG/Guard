using Guard.Core.Services;
using System.Runtime.CompilerServices;

namespace Guard.Core.Logging;

/// <summary>Одна запись Error для пробрасываемого объекта исключения; ожидаемые отказы не создают шторм ошибок.</summary>
public static class OperationFailureLogging
{
  private sealed class Reported { public bool Error; }
  private static readonly ConditionalWeakTable<Exception, Reported> reported = new();
  public static void LogOperationFailure(this ILogger logger, Exception error, string template, params object?[] args)
    => LogOperationFailure(logger, error, CancellationToken.None, template, args);
  public static void LogOperationFailure(this ILogger logger, Exception error, CancellationToken operationCancellation, string template, params object?[] args)
  {
    var level = error switch {
      OperationCanceledException when operationCancellation.IsCancellationRequested => LogLevel.Debug,
      EntityRuleException or EntityConflictException or UnauthorizedAccessException or KeyNotFoundException => LogLevel.Warning,
      ArgumentException when error is not ArgumentNullException => LogLevel.Warning,
      _ => LogLevel.Error
    };
    if (!logger.IsEnabled(level)) return;
    if (level != LogLevel.Error) { logger.Log(level, error, template, args); return; }
    var state = reported.GetValue(error, _ => new());
    lock (state) {
      if (state.Error) { logger.LogDebug("Уже зарегистрированный сбой на следующем этапе {Operation}", template); return; }
      logger.LogError(error, template, args); state.Error = true;
    }
  }
}
