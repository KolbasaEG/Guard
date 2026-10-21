using Guard.Core.Entities;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;

namespace Guard.Components.Pages.Synchronization;

public partial class ActualityPanel : IAsyncDisposable
{
  [Inject] protected ISynchronizationService Service { get; set; } = default!;
  [Inject] protected ILogger<ActualityPanel> Logger { get; set; } = default!;
  [Parameter, EditorRequired] public string ReadPermission { get; set; } = "";
  [Parameter] public EventCallback<bool> ApiModeChanged { get; set; }
  [Parameter] public Func<Task<bool>>? ReloadData { get; set; }
  [CascadingParameter] public UserAccessSnapshot? Access { get; set; }
  private DirectoryActuality? actuality;
  private string? error;
  private bool disposed;
  private bool? lastMode;
  private bool hasSuccessfulCheck;
  private DateTime? lastAppliedAt;
  private readonly CancellationTokenSource lifetime = new();
  private readonly SemaphoreSlim refresh = new(1, 1);
  private Task? polling;

  protected override async Task OnParametersSetAsync()
  {
    if (Access != null) await RefreshAsync();
  }
  protected override Task OnAfterRenderAsync(bool firstRender)
  {
    if (firstRender) polling = PollAsync();
    return Task.CompletedTask;
  }
  private async Task PollAsync()
  {
    try
    {
      using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
      while (await timer.WaitForNextTickAsync(lifetime.Token))
        await InvokeAsync(async () => { await RefreshAsync(); if (!disposed) StateHasChanged(); });
    }
    catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
  }
  protected async Task RefreshAsync()
  {
    if (disposed || Access == null) return;
    try { await refresh.WaitAsync(lifetime.Token); }
    catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { return; }
    try
    {
      var result = await Service.GetActualityAsync(ReadPermission, lifetime.Token);
      if (disposed) return;
      var changed = hasSuccessfulCheck && result.ActualAt.HasValue && lastAppliedAt != result.ActualAt;
      actuality = result; error = null;
      var api = result.Mode == DataManagementMode.Api;
      if (lastMode != api) { lastMode = api; await ApiModeChanged.InvokeAsync(api); }
      if (changed && (ReloadData == null || !await ReloadData()))
      {
        error = "Не удалось обновить таблицу. Загрузка будет повторена автоматически.";
        return;
      }
      if (disposed) return;
      lastAppliedAt = result.ActualAt;
      hasSuccessfulCheck = true;
    }
    catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    catch (Exception ex)
    {
      actuality = null; error = "Не удалось проверить актуальность данных.";
      Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(Logger, ex, "Получение общей актуальности справочников");
      if (lastMode != true) { lastMode = true; await ApiModeChanged.InvokeAsync(true); }
    }
    finally { refresh.Release(); }
  }
  public async ValueTask DisposeAsync()
  {
    disposed = true; await lifetime.CancelAsync();
    if (polling != null) await polling;
    lifetime.Dispose();
  }
}
