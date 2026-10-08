using Microsoft.JSInterop;

namespace Guard.Components.Library;

public sealed class BrowserTimeService(IJSRuntime js)
{
  public TimeZoneInfo? Zone { get; private set; }
  private Task? initialization;
  public Task InitializeAsync() => initialization ??= LoadAsync();
  private async Task LoadAsync()
  {
    try {
      await using var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/browser-time.js");
      Zone = TimeZoneInfo.FindSystemTimeZoneById(await module.InvokeAsync<string>("timeZone"));
    }
    catch { initialization = null; throw; }
  }
  public DateTime ToUtc(DateTime local)
  {
    if (local.Kind == DateTimeKind.Utc) return local;
    var zone = Zone ?? throw new InvalidOperationException("Часовой пояс браузера ещё не определён. Повторите действие после загрузки страницы.");
    return ConvertToUtc(local, zone);
  }
  public static DateTime ConvertToUtc(DateTime local, TimeZoneInfo zone)
  {
    local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
    if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local))
      throw new ArgumentException("В выбранном часовом поясе время отсутствует или неоднозначно. Выберите другое время.");
    return TimeZoneInfo.ConvertTimeToUtc(local, zone);
  }
  public string Format(DateTime? utc, string format = "dd.MM.yyyy HH:mm") => utc == null ? "—" :
    Zone == null ? "Загрузка времени…" : TimeZoneInfo.ConvertTimeFromUtc(
      utc.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc) : utc.Value.ToUniversalTime(), Zone).ToString(format);
}
