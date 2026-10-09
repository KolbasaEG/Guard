using Guard.Core.Enums;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Guard.Components.Pages.Administrator.Personals;

public partial class Details : IDisposable
{
  [Inject] protected IPersonalService Service { get; set; } = default!;
  [Inject] protected DialogService Dialogs { get; set; } = default!;
  [Inject] protected NotificationService Notifications { get; set; } = default!;
  [Inject] protected ILogger<Details> Logger { get; set; } = default!;
  [Parameter] public Guid Id { get; set; }

  private readonly CancellationTokenSource cts = new();
  private CancellationTokenSource? loading;
  private Guid? loadedId;
  private bool disposed;
  protected PersonalDetailsDto? item;
  protected bool isLoading;
  protected string? error;

  protected override async Task OnParametersSetAsync()
  {
    if (disposed || loadedId == Id) return;
    loadedId = Id;
    loading?.Cancel();
    var request = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
    loading = request;
    isLoading = true;
    item = null;
    error = null;
    try
    {
      var result = await Service.GetDetailsAsync(Id, request.Token);
      request.Token.ThrowIfCancellationRequested();
      if (disposed || !ReferenceEquals(loading, request)) return;
      item = result ?? throw new KeyNotFoundException("Сотрудник недоступен.");
    }
    catch (OperationCanceledException) when (request.IsCancellationRequested) { }
    catch (Exception ex)
    {
      if (!disposed && ReferenceEquals(loading, request))
      {
        Logger.LogWarning(ex, "Загрузка карточки сотрудника");
        error = UserOperationErrors.Message(ex);
        Notifications.Notify(new NotificationMessage
        {
          Severity = NotificationSeverity.Error, Summary = "Сотрудник", Detail = error,
          Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
        });
      }
    }
    finally
    {
      if (ReferenceEquals(loading, request)) { loading = null; isLoading = false; }
      request.Dispose();
    }
  }

  protected Task OpenInfoAsync()
  {
    if (disposed || item == null) return Task.CompletedTask;
    return Dialogs.OpenAsync<DetailsInfo>("", new Dictionary<string, object?> { ["Item"] = item },
      new DialogOptions { Width = "650px", ShowTitle = false, ContentCssClass = "rz-p-1" });
  }

  protected void Close()
  {
    if (disposed) return;
    cts.Cancel();
    Dialogs.Close(null);
  }

  protected static string Display(object? value) => value == null || value is string text && string.IsNullOrWhiteSpace(text)
    ? "—" : value.ToString() ?? "—";

  public static string StatusName(Status status) => status switch
  {
    Status.Inserted => "Добавлен",
    Status.Modified => "Изменён",
    Status.Blocked => "Заблокирован",
    Status.Archived => "В архиве",
    Status.ArchivedBlocked => "В архиве, заблокирован",
    Status.Deleted => "Удалён",
    _ => "Неизвестное состояние"
  };

  public void Dispose()
  {
    if (disposed) return;
    disposed = true;
    cts.Cancel();
    cts.Dispose();
  }
}
