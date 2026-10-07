
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
namespace Guard.Components.Pages.Administrator.Personals;
public partial class Index : IDisposable
{
  [Inject] public IPersonalService Service { get; set; } = default!;
  [Inject] public DialogService Dialogs { get; set; } = default!;
  [Inject] public IJSRuntime JS { get; set; } = default!;
  [Inject] public ILogger<Index> Logger { get; set; } = default!;
  [CascadingParameter] public UserAccessSnapshot? Access { get; set; }
  private readonly CancellationTokenSource cts = new();
  protected RadzenDataGrid<PersonalListItemDto> grid = default!;
  protected IReadOnlyList<PersonalListItemDto> items = [];
  protected int count;
  protected bool busy;
  protected string? search, error, sort;
  protected DataViewMode mode = DataViewMode.Active;
  private PersonalSearchRequest Request(int skip = 0, int take = 20) => new(search, Mode: mode, Skip:skip, Take:take, OrderBy:sort);
  protected bool CanWrite(PersonalListItemDto row) => Access?.Has(Permissions.Personals.Write) == true;
  protected async Task LoadData(LoadDataArgs args)
  {
    busy = true; error = null; sort = args.OrderBy;
    try { var result = await Service.SearchAsync(Request(args.Skip ?? 0, args.Top ?? 20), cts.Token); items = result.Items; count = result.Count; }
    catch (OperationCanceledException) { }
    catch (Exception ex) { items = []; count = 0; Logger.LogWarning(ex, "Загрузка списка сотрудников"); error = "Не удалось загрузить список. Проверьте доступ."; }
    finally { busy = false; }
  }
  protected Task ReloadAsync() => grid.Reload();
  private async Task OpenDialog<T>(Guid? id = null) where T : IComponent
  {
    var args = id.HasValue ? new Dictionary<string,object> { ["Id"] = id.Value } : null;
    var result = await Dialogs.OpenAsync<T>("", args, new DialogOptions { Width="850px", ShowTitle=false });
    if (result != null) await ReloadAsync();
  }
  protected Task OpenRow(PersonalListItemDto row) => Access?.Has(Permissions.Personals.ReadDetails) == true ? DetailsAsync(row) : Task.CompletedTask;
  protected Task DetailsAsync(PersonalListItemDto row) => OpenDialog<Details>(row.Id);
  protected Task EditAsync(PersonalListItemDto row) => OpenDialog<Edit>(row.Id);
  protected Task AddAsync() => OpenDialog<Add>();
  protected Task IpsAsync(PersonalListItemDto row) => Dialogs.OpenAsync<IpAssignments>("Разрешённые IP",
    new Dictionary<string,object> { ["PersonalId"]=row.Id }, new DialogOptions { Width="650px" });
  protected async Task ChangeStatusAsync(PersonalListItemDto row, bool restore)
  {
    if (await Dialogs.Confirm(restore ? "Извлечь запись из архива?" : "Поместить запись в архив?", "Изменение статуса",
      new ConfirmOptions { OkButtonText="Да", CancelButtonText="Отмена" }) != true) return;
    try { if (restore) await Service.RestoreAsync(row.Id, cts.Token); else await Service.ArchiveAsync(row.Id, cts.Token); await ReloadAsync(); }
    catch (Exception ex) { Logger.LogWarning(ex, "Изменение статуса сотрудника"); error = "Не удалось изменить статус записи."; }
  }
  protected async Task ExportAsync()
  {
    try {
      var bytes = await Service.ExportAsync(Request(), cts.Token);
      await using var module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/download.js");
      using var stream = new MemoryStream(bytes);
      using var reference = new DotNetStreamReference(stream);
      await module.InvokeVoidAsync("downloadFile", "Сотрудники.csv", reference);
    }
    catch (Exception ex) { Logger.LogWarning(ex, "Экспорт сотрудников"); error = "Экспорт недоступен."; }
  }
  public void Dispose() { cts.Cancel(); cts.Dispose(); }
}
