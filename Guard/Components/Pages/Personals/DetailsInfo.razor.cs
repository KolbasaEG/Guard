using Guard.Components.Library;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;

namespace Guard.Components.Pages.Personals;

public partial class DetailsInfo : IDisposable
{
  [Parameter, EditorRequired] public PersonalDetailsDto Item { get; set; } = default!;
  [Inject] protected BrowserTimeService Time { get; set; } = default!;
  [Inject] protected ILogger<DetailsInfo> Logger { get; set; } = default!;
  protected string? timeError;
  private bool disposed;

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    if (!firstRender) return;
    try { await Time.InitializeAsync(); }
    catch (Exception ex)
    {
      if (disposed) return;
      Logger.LogWarning(ex, "Определение часового пояса карточки сотрудника");
      timeError = "Не удалось определить часовой пояс браузера. Откройте дополнительную информацию повторно.";
    }
    if (!disposed) StateHasChanged();
  }

  protected string FormatDate(DateTime? value) => value == null ? "—" : timeError != null ? "Время недоступно" : Time.Format(value);
  protected static string Display(object? value) => value == null || value is string text && string.IsNullOrWhiteSpace(text)
    ? "—" : value.ToString() ?? "—";

  protected IEnumerable<KeyValuePair<string, object?>> Identifiers()
  {
    yield return new("Идентификатор сотрудника", Item.Id);
    yield return new("Подразделение", Item.SubdivisionId);
    yield return new("Локальный номер сотрудника", Item.PersonalId);
    yield return new("Сотрудник во внешней системе", Item.ExternalId);
    yield return new("Подразделение во внешней системе", Item.PersonalSubdivisionId);
    yield return new("Учётная запись", Item.UserId);
    yield return new("Тип категории персонала", Item.PersonnelCategoryType);
    yield return new("Код категории персонала", Item.PersonnelCategoryCode);
    yield return new("Тип звания", Item.SpecialRankType);
    yield return new("Код звания", Item.SpecialRankCode);
    yield return new("Тип должности", Item.PositionType);
    yield return new("Код должности", Item.PositionCode);
    yield return new("Тип категории работника", Item.WorkerCategoryType);
    yield return new("Код категории работника", Item.WorkerCategoryCode);
    yield return new("Тип статуса во внешней системе", Item.StatusType);
    yield return new("Код статуса во внешней системе", Item.StatusCode);
  }

  public void Dispose() => disposed = true;
}
