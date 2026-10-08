using Serilog.Core;
using Serilog.Events;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Guard.Core.Services;

public class LogLevelState
{
  public LogEventLevel MinimumLevel { get; set; } = LogEventLevel.Information;
  public int MaxSessions { get; set; } = 100;
}

public class LogLevelPersistenceService
{
  private const string FilePath = "logsettings.json";

  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter() } // Поддержка строковых и числовых enum
  };

  public static LogLevelState LoadState()
  {
    if (!File.Exists(FilePath)) return new LogLevelState();

    try
    {
      var json = File.ReadAllText(FilePath);
      var state = JsonSerializer.Deserialize<LogLevelState>(json, JsonOptions);
      return state != null && state.MaxSessions is >= 1 and <= 10000 && Enum.IsDefined(state.MinimumLevel) ? state : new LogLevelState();
    }
    catch
    {
      return new LogLevelState();
    }
  }

  public static void SaveState(LoggingLevelSwitch levelSwitch, int maxSessions)
  {
    var state = new LogLevelState
    {
      MinimumLevel = levelSwitch.MinimumLevel,
      MaxSessions = maxSessions
    };

    SaveState(state);
  }

  public static void SaveState(LogLevelState state)
  {
    var path = Path.GetFullPath(FilePath);
    var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
    try {
      File.WriteAllText(temporary, JsonSerializer.Serialize(state, JsonOptions));
      File.Move(temporary, path, overwrite: true);
    }
    finally { if (File.Exists(temporary)) File.Delete(temporary); }
  }
}
