using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Guard.Components.Pages.Audit;

public partial class Details : IAsyncDisposable
{
  [Parameter] public Guid EventId { get; set; }
  [Inject] protected IAuditSearchService Journal { get; set; } = default!;
  [Inject] protected DialogService Dialogs { get; set; } = default!;
  [Inject] protected ILogger<Details> Logger { get; set; } = default!;
  private readonly CancellationTokenSource lifetime = new();
  private CancellationTokenSource? request;
  protected AuditDetailsDto? data;
  protected bool loading;
  private bool disposed;

  protected override async Task OnParametersSetAsync()
  {
    request?.Cancel();
    using var current = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
    request = current; loading = true; data = null;
    try {
      var result = await Journal.GetAsync(EventId, current.Token);
      if (!disposed && ReferenceEquals(request, current) && !current.IsCancellationRequested) data = result;
    }
    catch (OperationCanceledException) when (current.IsCancellationRequested) { }
    catch (Exception error) { Logger.LogError(error, "Загрузка события аудита {EventId}", EventId); }
    finally { if (ReferenceEquals(request, current)) { request = null; loading = false; } }
  }
  protected void Close() => Dialogs.Close(null);
  public ValueTask DisposeAsync()
  {
    disposed = true; lifetime.Cancel(); request?.Cancel(); lifetime.Dispose(); return ValueTask.CompletedTask;
  }
}
