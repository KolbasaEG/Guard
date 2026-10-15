using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace Guard.Components.Layout.Navigation;

public partial class AppNavigation : IAsyncDisposable
{
  [Inject] protected NavigationManager Navigation { get; set; } = default!;
  [Inject] protected IJSRuntime JS { get; set; } = default!;
  [Inject] protected ILogger<AppNavigation> Logger { get; set; } = default!;
  [CascadingParameter] public UserAccessSnapshot? Access { get; set; }

  private IReadOnlyList<NavigationGroup> groups = [];
  private readonly HashSet<string> favorites = new(StringComparer.OrdinalIgnoreCase);
  private readonly CancellationTokenSource lifetime = new();
  private IJSObjectReference? preferencesModule;
  private Task<IJSObjectReference>? moduleTask;
  private string? preferencesUserId, loadedUserId, loadingUserId, expandedGroup, previousPath, storageNotice;
  private int preferencesVersion;
  private bool disposed;
  protected string search = string.Empty;
  private string? CurrentPath => NavigationCatalog.Find(Navigation.ToBaseRelativePath(Navigation.Uri))?.Path;
  private bool PreferencesReady => !string.IsNullOrEmpty(preferencesUserId) && loadedUserId == preferencesUserId;
  private bool HasSearch => !string.IsNullOrWhiteSpace(search);
  private NavigationItem? HomeItem => NavigationCatalog.Groups.SelectMany(group => group.Items)
    .FirstOrDefault(item => item.Path == "/" && item.IsVisible(Access));
  private IEnumerable<NavigationItem> FavoriteItems => groups.SelectMany(group => group.Items).Where(item => favorites.Contains(item.Path));
  private int SelectedGroupIndex => groups.ToList().FindIndex(group => group.Text == expandedGroup);
  private IReadOnlyList<NavigationGroup> SearchGroups => groups.Select(group => new NavigationGroup(group.Text,
      group.Items.Where(item => Matches(item.Text, search) || Matches(group.Text, search)).ToArray(), group.Icon))
    .Where(group => group.Items.Count > 0).ToArray();

  protected override void OnInitialized() => Navigation.LocationChanged += LocationChanged;

  protected override void OnParametersSet()
  {
    var userId = Access?.UserId;
    if (preferencesUserId != userId)
    {
      preferencesUserId = userId;
      loadedUserId = loadingUserId = expandedGroup = null;
      preferencesVersion++;
      favorites.Clear();
      search = string.Empty;
      storageNotice = null;
      previousPath = null;
    }
    groups = NavigationCatalog.VisibleGroups(Access)
      .Where(group => group.Items.Any(item => item.Path != "/"))
      .Select(group => new NavigationGroup(group.Text, group.Items.Where(item => item.Path != "/").ToArray(), group.Icon)).ToArray();
    RevealCurrentGroup();
  }

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    if (!RendererInfo.IsInteractive || disposed || string.IsNullOrEmpty(preferencesUserId) || PreferencesReady || loadingUserId == preferencesUserId) return;
    var userId = preferencesUserId;
    var version = preferencesVersion;
    loadingUserId = userId;
    try
    {
      preferencesModule ??= await (moduleTask ??= JS.InvokeAsync<IJSObjectReference>("import", lifetime.Token, "./js/navigation-preferences.js").AsTask());
      var paths = await preferencesModule.InvokeAsync<string[]>("load", lifetime.Token, userId);
      if (disposed || version != preferencesVersion) return;
      var knownPaths = NavigationCatalog.Groups.SelectMany(group => group.Items).Where(item => item.Path != "/").Select(item => item.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
      favorites.UnionWith((paths ?? []).Where(knownPaths.Contains));
    }
    catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    catch (JSDisconnectedException) { }
    catch (JSException ex)
    {
      Logger.LogWarning(ex, "Не удалось загрузить избранные разделы меню");
      if (!disposed && version == preferencesVersion) storageNotice = "Избранное доступно до закрытия страницы.";
    }
    finally
    {
      if (!disposed && version == preferencesVersion)
      {
        loadedUserId = userId;
        loadingUserId = null;
        StateHasChanged();
      }
    }
  }

  protected void SearchChanged(string value) => search = value ?? string.Empty;

  protected async Task ToggleFavoriteAsync(NavigationItem item)
  {
    if (disposed || item.Path == "/" || !groups.SelectMany(group => group.Items).Any(visible => visible.Path == item.Path)) return;
    if (!favorites.Remove(item.Path)) favorites.Add(item.Path);
    if (preferencesModule == null || !PreferencesReady) return;
    var version = preferencesVersion;
    try
    {
      var saved = await preferencesModule.InvokeAsync<bool>("save", lifetime.Token, preferencesUserId, favorites.ToArray());
      if (disposed || version != preferencesVersion) return;
      storageNotice = saved ? null : "Браузер не разрешает сохранение. Избранное доступно до закрытия страницы.";
    }
    catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    catch (JSDisconnectedException) { }
    catch (JSException ex)
    {
      Logger.LogWarning(ex, "Не удалось сохранить избранные разделы меню");
      if (!disposed && version == preferencesVersion) storageNotice = "Избранное доступно до закрытия страницы.";
    }
  }

  private void SelectGroup(int index) => expandedGroup = index >= 0 && index < groups.Count ? groups[index].Text : null;
  private static bool Matches(string text, string query) => text.Replace('ё', 'е').Replace('Ё', 'Е')
    .Contains(query.Trim().Replace('ё', 'е').Replace('Ё', 'Е'), StringComparison.OrdinalIgnoreCase);

  private void RevealCurrentGroup()
  {
    var path = CurrentPath;
    if (previousPath != path || expandedGroup != null && !groups.Any(group => group.Text == expandedGroup))
      expandedGroup = groups.FirstOrDefault(group => group.Items.Any(item => item.Path == path))?.Text;
    previousPath = path;
  }

  private void LocationChanged(object? sender, LocationChangedEventArgs args)
  {
    _ = InvokeAsync(() => {
      if (disposed) return;
      search = string.Empty;
      RevealCurrentGroup();
      StateHasChanged();
    });
  }

  public async ValueTask DisposeAsync()
  {
    disposed = true;
    Navigation.LocationChanged -= LocationChanged;
    lifetime.Cancel();
    if (preferencesModule != null)
    {
      try { await preferencesModule.DisposeAsync(); }
      catch (JSDisconnectedException) { }
    }
    lifetime.Dispose();
  }
}
