using Guard.Core.Services.DTOs;
using Serilog.Events;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Guard.Core.Logging;

/// <summary>Состояние одного экземпляра. Не зависит от основной БД и доступности аудита.</summary>
public sealed class TechnicalLogRuntime
{
  private readonly object gate = new();
  private readonly string path;
  private readonly TimeProvider clock;
  private TechnicalLogSettings settings = new();
  private DiagnosticLogSession? session;
  private readonly List<DiagnosticLogSession> expirations = [];
  private Guid version = Guid.NewGuid();
  private string? configurationError;
  private static readonly JsonSerializerOptions json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
  public string InstanceId { get; } = Environment.MachineName + ":" + Environment.ProcessId;
  public TechnicalLogRuntime(string path = "technical-logsettings.json", TimeProvider? clock = null)
  {
    this.path = Path.GetFullPath(path); this.clock = clock ?? TimeProvider.System;
    try {
      if (File.Exists(this.path)) {
        var state = JsonSerializer.Deserialize<Persisted>(File.ReadAllText(this.path), json) ?? throw new InvalidDataException();
        if (state.Settings == null || state.Version == Guid.Empty) throw new InvalidDataException();
        state.Settings.Validate(); ValidateSession(state.Session, state.Settings);
        settings = state.Settings; session = state.Session; version = state.Version;
        if (state.Expirations is { Length: > 64 } || state.Expirations?.Any(e => e == null || e.Id == Guid.Empty) == true) throw new InvalidDataException();
        expirations.AddRange(state.Expirations ?? []);
      }
    } catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException) {
      configurationError = error.GetType().Name;
      // Небезопасную или повреждённую подробную сессию не восстанавливаем.
      settings = new(); session = null; expirations.Clear();
    }
  }
  public (Guid Version, TechnicalLogSettings Settings, DiagnosticLogSession? Session) Read()
  { lock (gate) return (version, settings, ActiveSession()); }
  public string? ConfigurationError { get { lock (gate) return configurationError; } }
  public DateTime UtcNow => clock.GetUtcNow().UtcDateTime;
  private DiagnosticLogSession? ActiveSession() => session?.ExpiresAtUtc > UtcNow ? session : null;
  public bool Accept(LogEvent entry)
  {
    lock (gate) {
      var active = ActiveSession();
      var category = entry.Properties.TryGetValue("SourceContext", out var value) ? (value as ScalarValue)?.Value as string : null;
      var normal = (category?.StartsWith("Microsoft.", StringComparison.Ordinal) == true || category?.StartsWith("System.", StringComparison.Ordinal) == true)
        && settings.NormalLevel < ApplicationLogLevel.Warning ? ApplicationLogLevel.Warning : settings.NormalLevel;
      var threshold = active != null && Matches(category, active.Category) ? active.Level : normal;
      return (int)entry.Level >= (int)threshold;
    }
  }
  public static bool Matches(string? category, string prefix) => prefix.Length == 0 || category == prefix ||
    category?.StartsWith(prefix + ".", StringComparison.Ordinal) == true;
  public DiagnosticLogSession? SessionFor(string? category)
  { lock (gate) { var active = ActiveSession(); return active != null && Matches(category, active.Category) ? active : null; } }
  public void Change(Guid expected, TechnicalLogSettings value, DiagnosticLogSession? diagnostic)
  {
    ArgumentNullException.ThrowIfNull(value); value.Validate(); ValidateSession(diagnostic, value);
    lock (gate) {
      if (version != expected) throw new InvalidOperationException("Настройки уже изменены. Обновите состояние и повторите действие.");
      if (diagnostic != null && diagnostic.ExpiresAtUtc > UtcNow.AddMinutes(value.MaximumDiagnosticMinutes))
        throw new ArgumentException("Срок диагностики превышает установленный предел.");
      QueueExpired();
      var next = new Persisted(Guid.NewGuid(), value, diagnostic, expirations.ToArray());
      Save(next); settings = value; session = diagnostic; version = next.Version; configurationError = null;
    }
  }
  public DiagnosticLogSession? Expire()
  {
    lock (gate) {
      if (session == null || session.ExpiresAtUtc > UtcNow) return null;
      var expired = session;
      QueueExpired();
      try { var next = new Persisted(Guid.NewGuid(), settings, null, expirations.ToArray()); Save(next); session = null; version = next.Version; configurationError = null; }
      catch (Exception error) when (error is IOException or UnauthorizedAccessException) { configurationError = error.GetType().Name; }
      return expired;
    }
  }
  private void QueueExpired()
  {
    if (session == null || session.ExpiresAtUtc > UtcNow || expirations.Any(e => e.Id == session.Id)) return;
    if (expirations.Count >= 64) throw new InvalidOperationException("Не подтверждены предыдущие завершения диагностики. Восстановите аудит.");
    expirations.Add(session);
  }
  public IReadOnlyList<DiagnosticLogSession> PendingExpirations() { lock (gate) return expirations.ToArray(); }
  public void ConfirmExpiration(Guid id)
  {
    lock (gate) {
      var remaining = expirations.Where(e => e.Id != id).ToArray();
      var active = session?.Id == id && session.ExpiresAtUtc <= UtcNow ? null : session;
      try {
        Save(new(version, settings, active, remaining)); session = active; expirations.Clear(); expirations.AddRange(remaining); configurationError = null;
      } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { configurationError = error.GetType().Name; }
    }
  }
  private void Save(Persisted value)
  {
    var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
    try {
      using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
        JsonSerializer.Serialize(stream, value, json); stream.Flush(flushToDisk: true);
      }
      File.Move(temporary, path, overwrite: true);
    } finally { if (File.Exists(temporary)) File.Delete(temporary); }
  }
  private static void ValidateSession(DiagnosticLogSession? value, TechnicalLogSettings settings)
  {
    if (value == null) return;
    if (value.Id == Guid.Empty || string.IsNullOrWhiteSpace(value.ActorId) ||
      value.Level is not (ApplicationLogLevel.Debug or ApplicationLogLevel.Verbose) ||
      value.Category == null || value.Category.Length > 200 || value.Category.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '.' and not '_') ||
      string.IsNullOrWhiteSpace(value.Reason) || value.Reason.Length > 500 ||
      value.StartedAtUtc.Kind != DateTimeKind.Utc || value.ExpiresAtUtc.Kind != DateTimeKind.Utc ||
      value.ExpiresAtUtc <= value.StartedAtUtc || value.ExpiresAtUtc - value.StartedAtUtc > TimeSpan.FromMinutes(settings.MaximumDiagnosticMinutes))
      throw new ArgumentException("Проверьте категорию, основание, уровень и длительность диагностики.");
  }
  private sealed record Persisted(Guid Version, TechnicalLogSettings Settings, DiagnosticLogSession? Session, DiagnosticLogSession[]? Expirations = null);
}
