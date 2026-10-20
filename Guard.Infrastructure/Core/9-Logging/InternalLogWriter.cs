using System.Text;

namespace Guard.Core.Logging;

/// <summary>Последний канал диагностики: его ошибки не должны вызывать ошибки приложения или рекурсию Serilog.</summary>
public sealed class InternalLogWriter(string directory = "logs")
{
  private readonly object gate = new();
  private const long MaximumBytes = 10 * 1024 * 1024;

  public void Write(string message)
  {
    var line = $"{DateTime.UtcNow:o} {message.TrimEnd()}{Environment.NewLine}";
    lock (gate) {
      try { Console.Error.Write(line); } catch { /* Последний доступный канал может быть закрыт. */ }
      try {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"serilog-internal-errors-{DateTime.UtcNow:yyyyMMdd}.log");
        if (File.Exists(path) && new FileInfo(path).Length + Encoding.UTF8.GetByteCount(line) > MaximumBytes)
          File.Move(path, Path.Combine(directory, $"serilog-internal-errors-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.log"));
        File.AppendAllText(path, line);
      }
      catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException) {
        // Console уже получила событие. Не вызываем логгер из обработчика SelfLog.
      }
    }
  }
}
