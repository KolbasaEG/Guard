using System.Reflection;
using Guard.Core.Enums;
using Guard.Core.Services;
using Guard.Core.Services.Notifications;
using Guard.Web.Notifications;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using NotificationIndex = Guard.Components.Pages.Notifications.Index;

// Настоящий цикл интерактивного рендера: HtmlRenderer не вызывает OnAfterRenderAsync.
#pragma warning disable BL0006
internal static class NotificationInitialLoadTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var service = new NotificationTests.ServiceStub();
    var first = new UserNotificationDto(Guid.NewGuid(), UserNotificationKind.ExportReady, UserNotificationSeverity.Success,
      "Первая запись", "Готово", NotificationTarget.Export, Guid.NewGuid(), false, false, DateTime.UtcNow, DateTime.UtcNow, 1, false);
    service.Rows.Add(first);
    await using var center = new NotificationCenterState(service, new NotificationTests.PermissionsStub("owner", false),
      new NotificationSignals(NullLogger<NotificationSignals>.Instance), NullLogger<NotificationCenterState>.Instance);
    // Центр уже загружен до перехода; опрос и Changed во время открытия списка отсутствуют.
    await center.RefreshAsync();
    var changes = 0; center.Changed += () => changes++;
    await using var services = new ServiceCollection().AddLogging()
      .AddSingleton<IUserNotificationService>(service).AddSingleton(center).AddSingleton<NotificationService>()
      .AddSingleton<IJSRuntime, Js>().AddSingleton<NavigationManager, Navigation>().AddScoped<DialogService>()
      .BuildServiceProvider();
    await using var renderer = new InteractiveRenderer(services, services.GetRequiredService<ILoggerFactory>());
    await renderer.Dispatcher.InvokeAsync(async () => {
      var (id, page) = await renderer.MountAsync();
      check(renderer.Errors.Count == 0, "notification page mounts with its real library wrapper and grid");
      var grid = Grid(page);
      check(service.Searches.Count > 0 && grid.Count == 1 && (grid.Data ?? []).Select(n => n.Id).SequenceEqual([first.Id]) && changes == 0,
        "opening notifications loads and renders records without refresh, polling or a center change");
      check(service.Searches.All(q => !q.UnreadOnly && q.Skip == 0 && q.Take == 20),
        "initial notification search uses the first page and the default filter");
      var requests = service.Searches.Count;
      typeof(ComponentBase).GetMethod("StateHasChanged", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null);
      check(service.Searches.Count == requests, "ordinary notification page rerender does not restart initial loading");
      renderer.Unmount(id);

      var second = first with { Id = Guid.NewGuid(), Title = "Вторая запись", Revision = 2 };
      service.Rows.Add(second);
      var (_, reopened) = await renderer.MountAsync();
      var reopenedGrid = Grid(reopened);
      check(service.Searches.Count > requests && reopenedGrid.Count == 2 && (reopenedGrid.Data ?? []).Any(n => n.Id == second.Id) && changes == 0,
        "returning to notifications loads the current records without waiting for the center poll");
      check(renderer.Errors.Count == 0, "notification initial loading and repeated navigation complete without render failures");
      await CheckBackgroundRefreshAsync(renderer, service, center, reopened, check);
    });
  }

  private static async Task CheckBackgroundRefreshAsync(InteractiveRenderer renderer, NotificationTests.ServiceStub service,
    NotificationCenterState center, NotificationIndex page, Action<bool, string> check)
  {
    var grid = Grid(page);
    await center.RefreshAsync();
    var searches = TableSearches(service);
    await center.RefreshAsync(); await center.RefreshAsync();
    check(TableSearches(service) == searches && grid.Count == 2 && !grid.IsLoading,
      "unchanged center polls leave the rendered notification table untouched");

    service.Rows.AddRange(Enumerable.Range(3, 25).Select(n => service.Rows[0] with { Id = Guid.NewGuid(), Revision = n, Title = $"Запись {n}" }));
    var gate = new TaskCompletionSource<NotificationPage>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.SearchOverride = (_, _) => gate.Task;
    await center.RefreshAsync();
    check(!Loading(page) && !grid.IsLoading && grid.Count == 2,
      "a pending background refresh preserves displayed rows without a loading overlay");
    var displayed = renderer.NextDisplay();
    service.SearchOverride = null;
    gate.SetResult(Page(service, 0));
    await displayed.WaitAsync(TimeSpan.FromSeconds(3));
    check(grid.Count == 27 && grid.Data!.Count() == 20 && !grid.IsLoading,
      "changed background data is rendered after the silent request completes");

    await grid.GoToPage(1);
    check(grid.CurrentPage == 1 && grid.Data!.Count() == 7, "notification paging remains available after background refresh");
    service.Rows[^1] = service.Rows[^1] with { IsRead = true };
    await center.RefreshAsync();
    check(grid.CurrentPage == 1 && grid.Data!.Any(n => n.Id == service.Rows[^1].Id && n.IsRead) && service.Searches[^1].Skip == 20,
      "background reading changes update the selected page without returning to page one");

    gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    service.SearchOverride = (_, _) => gate.Task;
    var manual = Call(page, "ReloadAsync");
    check(Loading(page) && grid.IsLoading, "manual notification refresh retains its loading overlay");
    var oldPage = Page(service, 20);
    searches = TableSearches(service);
    service.Rows[^1] = service.Rows[^1] with { Title = "Изменено во время ручной загрузки" };
    await center.RefreshAsync();
    check(TableSearches(service) == searches && Loading(page), "background invalidation does not cancel an active manual notification request");
    var deferred = new TaskCompletionSource<NotificationPage>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.SearchOverride = (_, _) => deferred.Task;
    displayed = renderer.NextDisplay(); gate.SetResult(oldPage);
    await displayed.WaitAsync(TimeSpan.FromSeconds(3));
    check(!Loading(page) && !grid.IsLoading, "manual loading overlay ends before a deferred background request finishes");
    service.SearchOverride = null; deferred.SetResult(Page(service, 20)); await manual;
    check(grid.CurrentPage == 1 && grid.Data!.Any(n => n.Title == "Изменено во время ручной загрузки") && !Loading(page) && !grid.IsLoading,
      "a change arriving during manual loading is applied by a deferred silent refresh");
    service.Rows.RemoveRange(20, 7);
    await center.RefreshAsync();
    check(grid.CurrentPage == 0 && grid.Count == 20 && grid.Data!.Count() == 20 && !grid.IsLoading,
      "removing the final page selects the last existing page without a background loading overlay");
    service.Rows.AddRange(Enumerable.Range(21, 7).Select(n => service.Rows[0] with { Id = Guid.NewGuid(), Revision = n, Title = $"Запись {n}" }));
    await center.RefreshAsync(); await Call(page, "FilterAsync", true); await grid.GoToPage(1);
    await center.MarkAllReadAsync();
    check(grid.CurrentPage == 0 && grid.Count == 0 && !grid.Data!.Any() && !grid.IsLoading,
      "marking all unread notifications read on page two produces an empty first page");
    check(renderer.Errors.Count == 0, "foreground and background notification refresh complete without render errors");
  }

  private static int TableSearches(NotificationTests.ServiceStub service) => service.Searches.Count(q => q.AfterRevision == 0 && q.ThroughRevision == long.MaxValue);
  private static NotificationPage Page(NotificationTests.ServiceStub service, int skip) => new(service.Rows.Skip(skip).Take(20).ToArray(), service.Rows.Count);
  private static bool Loading(NotificationIndex page) => (bool)typeof(NotificationIndex).GetField("loading", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!;
  private static Task Call(NotificationIndex page, string method, params object?[] args) => (Task)typeof(NotificationIndex).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, args)!;

  private static RadzenDataGrid<UserNotificationDto> Grid(NotificationIndex page) =>
    (RadzenDataGrid<UserNotificationDto>)typeof(NotificationIndex).GetField("grid", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!;

  private sealed class InteractiveRenderer(IServiceProvider services, ILoggerFactory logger) : Renderer(services, logger)
  {
    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
    public List<Exception> Errors { get; } = [];
    private TaskCompletionSource? nextDisplay;
    protected override void HandleException(Exception exception) => Errors.Add(exception);
    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch)
    {
      nextDisplay?.TrySetResult(); nextDisplay = null;
      return Task.CompletedTask;
    }
    public Task NextDisplay() => (nextDisplay = new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
    public async Task<(int Id, NotificationIndex Page)> MountAsync()
    {
      var page = (NotificationIndex)InstantiateComponent(typeof(NotificationIndex));
      var id = AssignRootComponentId(page);
      await RenderRootComponentAsync(id, ParameterView.Empty);
      return (id, page);
    }
    public void Unmount(int id) => RemoveRootComponent(id);
  }

  private sealed class Navigation : NavigationManager
  {
    public Navigation() => Initialize("http://localhost/", "http://localhost/Notifications");
    protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
  }

  private sealed class Js : IJSRuntime, IJSObjectReference
  {
    public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) =>
      ValueTask.FromResult(typeof(T) == typeof(IJSObjectReference) ? (T)(object)this : default!);
    public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken ct, object?[]? args) => InvokeAsync<T>(identifier, args);
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
  }
}
#pragma warning restore BL0006
