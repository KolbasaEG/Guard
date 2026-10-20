using Guard.Core.Services;
using Microsoft.AspNetCore.Components;

namespace Guard.Components.Pages.Audit;

public partial class Settings : IAsyncDisposable
{
  [Inject] private IAuditAdministrationService Administration { get; set; } = default!;
  [Inject] private ILogger<Settings> Logger { get; set; } = default!;
  private readonly CancellationTokenSource lifetime = new();
  private Task? refresh;
  private AuditConfiguration? model;
  private AuditSystemState? state;
  private bool allowed, loading = true, saving, transferPending, success;
  private string? message;
  protected override async Task OnInitializedAsync() => await ReloadAsync();
  protected override Task OnAfterRenderAsync(bool firstRender) {
    if (firstRender) refresh = PollAsync();
    return Task.CompletedTask;
  }
  private async Task ReloadAsync() {
    loading = true;
    try {
      var current = await Administration.GetStateAsync(lifetime.Token);
      state = current; allowed = true;
      try { model = await Administration.GetConfigurationAsync(lifetime.Token); transferPending = false; }
      catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
      catch (Exception error) { Logger.LogWarning(error, "Настройки аудита недоступны"); model = null; message = "Настройки недоступны. Состояние локального хранилища показано выше."; }
    }
    catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    catch (Exception error) { Failed(error); }
    finally { loading = false; }
  }
  private async Task PollAsync() {
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
    try {
      while (await timer.WaitForNextTickAsync(lifetime.Token)) {
        if (saving || loading) continue;
        try { state = await Administration.GetStateAsync(lifetime.Token); allowed = true; }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { break; }
        catch (Exception error) { Failed(error); }
        await InvokeAsync(StateHasChanged);
      }
    } catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
  }
  private async Task SaveAsync() {
    if (model == null || saving) return;
    saving = true; success = false;
    try { await Administration.SaveAsync(model, transferPending, lifetime.Token); await ReloadAsync(); success = true; message = "Настройки сохранены с обязательным аудитом."; }
    catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    catch (ArgumentException error) { message = error.Message; }
    catch (InvalidOperationException error) { message = error is AuditUnavailableException ? "Локальная запись аудита недоступна. Настройки не подтверждены." : error.Message; }
    catch (Exception error) { Failed(error); }
    finally { saving = false; }
  }
  private async Task TestAsync() {
    if (model == null || saving) return;
    saving = true; success = false;
    try { var result = await Administration.TestConnectionAsync(model, lifetime.Token); success = result.Succeeded; message = result.Message; }
    catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    catch (ArgumentException error) { message = error.Message; }
    catch (Exception error) { Failed(error); }
    finally { saving = false; }
  }
  private void Failed(Exception error) {
    Logger.LogWarning(error, "Диагностика и настройки аудита недоступны");
    allowed = false; state = null; success = false; message = "Не удалось подтвердить доступ или получить состояние. При недоступной Identity-БД требуется обслуживание сервера.";
  }
  private static string Format(DateTime? value) => value?.ToString("dd.MM.yyyy HH:mm:ss") ?? "Нет данных";
  private static string FormatBytes(long value) => value >= 1048576 ? $"{value:N0} байт ({value / 1048576d:N2} МиБ)" : $"{value:N0} байт";
  public async ValueTask DisposeAsync() {
    lifetime.Cancel(); if (refresh != null) await refresh; lifetime.Dispose();
  }
}
