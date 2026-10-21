using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Guard.Components.Pages.Logs;

public partial class TechnicalLoggingPanel : IAsyncDisposable
{
  [Inject] public ITechnicalLogService Service { get; set; } = default!;
  [Inject] public ILogger<TechnicalLoggingPanel> Logger { get; set; } = default!;
  [Inject] public NotificationService Notifications { get; set; } = default!;
  private TechnicalLogState? state;
  private TechnicalLogSettings editor = new();
  private Guid editorVersion;
  private IReadOnlyList<TechnicalErrorGroup>? groups;
  private string? error;
  private bool busy, disposed;
  private long generation;
  private readonly CancellationTokenSource stopping = new();
  private Task? polling;
  protected override async Task OnInitializedAsync() => await Reload();
  protected override Task OnAfterRenderAsync(bool firstRender) { if (firstRender) polling = Poll(); return Task.CompletedTask; }
  private async Task Poll()
  {
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
    try {
      while (await timer.WaitForNextTickAsync(stopping.Token)) await InvokeAsync(async () => {
        if (disposed || busy) return;
        var revision = generation;
        try { var next = await Service.GetAsync(stopping.Token); if (!disposed && revision == generation) { state = next; error = null; StateHasChanged(); } }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
        catch (Exception ex) { if (!disposed && revision == generation) { state = null; error = "Состояние недоступно. Проверьте доступ и аудит, затем обновите страницу."; Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(Logger, ex, "Чтение состояния технического логирования"); StateHasChanged(); } }
      });
    } catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
  }
  private Task Reload() => Run(async () => { var next = await Service.GetAsync(stopping.Token); Apply(next); });
  private void Apply(TechnicalLogState value) { if (disposed) return; state = value; editor = value.Settings; editorVersion = value.Version; }
  private Task Save(TechnicalLogSettings value) => Run(async () => { Apply(await Service.SaveAsync(editorVersion, value, stopping.Token)); Success("Настройки технического логирования сохранены."); });
  private Task Start(DiagnosticLogRequest value) => Run(async () => { Apply(await Service.StartAsync(state!.Version, value, stopping.Token)); Success("Временная диагностика включена. Проверьте срок завершения."); });
  private Task Stop() => Run(async () => { Apply(await Service.StopAsync(state!.Version, stopping.Token)); Success("Диагностика завершена. Действует штатный уровень."); });
  private void Success(string message) { if (!disposed) Notifications.Notify(new NotificationMessage { Severity = NotificationSeverity.Success,
    Summary = "Технические логи", Detail = message, Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;" }); }
  private Task LoadErrors() => Run(async () => { var result = await Service.ErrorsAsync(stopping.Token); if (!disposed) groups = result; });
  private async Task Run(Func<Task> action)
  {
    if (disposed || busy) return;
    busy = true; error = null; generation++;
    try { await action(); }
    catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
    catch (Exception ex) { if (!disposed) { error = UserOperationErrors.Message(ex); groups = null; Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(Logger, ex, "Управление техническим логированием"); } }
    finally { busy = false; }
  }
  public async ValueTask DisposeAsync() { if (disposed) return; disposed = true; stopping.Cancel(); if (polling != null) await polling; stopping.Dispose(); }
}
