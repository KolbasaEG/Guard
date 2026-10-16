using System.Reflection;
using Guard.Components.Library;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Radzen;

internal static class SynchronizationListLoadTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var time = new BrowserTimeService(new Js()); await time.InitializeAsync();
    var classifiers = DispatchProxy.Create<IClassifierService, QueryProxy>();
    var subdivisions = DispatchProxy.Create<ISubdivisionService, QueryProxy>();
    var personals = DispatchProxy.Create<IPersonalService, QueryProxy>();
    var pages = new (string Name, Page Page, QueryProxy Query)[] {
      ("classifiers", new ClassifiersPage(classifiers, time), (QueryProxy)(object)classifiers),
      ("subdivisions", new SubdivisionsPage(subdivisions, time), (QueryProxy)(object)subdivisions),
      ("personnel", new PersonalsPage(personals), (QueryProxy)(object)personals)
    };
    foreach (var (name, page, query) in pages)
    {
      var disposed = false;
      try
      {
        query.Next = _ => Task.FromException<object>(new IOException("Test query failure"));
        check(!await page.Reload(), $"{name}: actual LoadData reports query failure despite handled exception");
        query.Next = type => Task.FromResult(Empty(type));
        check(await page.Reload(), $"{name}: successful retry confirms data applied");
        var pending = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        Type? resultType = null;
        query.Next = type => { resultType = type; return pending.Task; };
        var stale = page.Reload();
        var oldToken = query.LastToken;
        query.Next = type => Task.FromResult(Empty(type));
        await page.LoadOther();
        check(oldToken.IsCancellationRequested, $"{name}: replacement cancels previous load");
        pending.SetResult(Empty(resultType!));
        check(!await stale, $"{name}: superseded synchronization load is not acknowledged");
        pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        query.Next = type => { resultType = type; return pending.Task; };
        var cancelled = page.Reload();
        oldToken = query.LastToken;
        page.Dispose();
        disposed = true;
        pending.SetResult(Empty(resultType!));
        check(oldToken.IsCancellationRequested && !await cancelled, $"{name}: disposed cancelled load is not acknowledged");
      }
      finally { if (!disposed) page.Dispose(); }
    }
  }
  private static object Empty(Type type) => type == typeof(PersonalSearchResult)
    ? new PersonalSearchResult([], 0)
    : type == typeof(SubdivisionSearchResult) ? new SubdivisionSearchResult([], 0)
    : Activator.CreateInstance(type, Activator.CreateInstance(type.GenericTypeArguments[0]), 0)!;

  public class QueryProxy : DispatchProxy
  {
    public Func<Type, Task<object>> Next { get; set; } = type => Task.FromResult(Empty(type));
    public CancellationToken LastToken { get; private set; }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
      if (method!.Name == nameof(ISubdivisionService.GetOwnSubdivisionPathAsync)) return Task.FromResult<string?>("/1/");
      LastToken = (CancellationToken)args![^1]!;
      return typeof(QueryProxy).GetMethod(nameof(Respond), BindingFlags.NonPublic | BindingFlags.Instance)!
        .MakeGenericMethod(method.ReturnType.GenericTypeArguments[0]).Invoke(this, null);
    }
    private async Task<T> Respond<T>() => (T)await Next(typeof(T));
  }
  private interface Page : IDisposable
  {
    Task<bool> Reload();
    Task LoadOther();
  }
  private sealed class ClassifiersPage : Guard.Components.Pages.Classifiers.Index, Page
  {
    public ClassifiersPage(IClassifierService service, BrowserTimeService time)
    { ClassifierService = service; Time = time; PermissionService = new PermissionsStub(); NotificationService = new(); Logger = NullLogger<Guard.Components.Pages.Classifiers.Index>.Instance; Access = new("root", true, []); }
    public Task<bool> Reload() => ReloadForSynchronizationAsync();
    public Task LoadOther() => LoadData(new() { Skip = 0, Top = 20 });
    protected override Task ReloadGridForSynchronizationAsync() => LoadOther();
  }
  private sealed class SubdivisionsPage : Guard.Components.Pages.Subdivisions.Index, Page
  {
    public SubdivisionsPage(ISubdivisionService service, BrowserTimeService time)
    { SubdivisionService = service; Time = time; NotificationService = new(); Logger = NullLogger<Guard.Components.Pages.Subdivisions.Index>.Instance; Access = new("root", true, []); }
    public Task<bool> Reload() => ReloadForSynchronizationAsync();
    public Task LoadOther() => LoadData(new() { Skip = 0, Top = 20 });
    protected override Task ReloadGridForSynchronizationAsync() => LoadOther();
  }
  private sealed class PersonalsPage : Guard.Components.Pages.Personals.Index, Page
  {
    public PersonalsPage(IPersonalService service)
    { Service = service; PermissionService = new PermissionsStub(); Notifications = new(); Logger = NullLogger<Guard.Components.Pages.Personals.Index>.Instance; Access = new("root", true, []); }
    public Task<bool> Reload() => ReloadForSynchronizationAsync();
    public Task LoadOther() => LoadData(new() { Skip = 0, Top = 20 });
    protected override Task ReloadGridForSynchronizationAsync() => LoadOther();
  }
  private sealed class PermissionsStub : IPermissionService
  {
    public Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) => Task.FromResult(new UserAccessSnapshot("root", true, []));
    public Task<UserAccessSnapshot> GetForUserAsync(string id, CancellationToken ct = default) => GetCurrentAsync(ct);
    public Task<bool> HasAsync(string permission, CancellationToken ct = default) => Task.FromResult(true);
    public Task RequireAsync(string permission, CancellationToken ct = default) => Task.CompletedTask;
  }
  private sealed class Js : IJSRuntime, IJSObjectReference
  {
    public ValueTask<T> InvokeAsync<T>(string id, object?[]? args) => ValueTask.FromResult((T)(id == "import" ? (object)this : "Europe/Moscow"));
    public ValueTask<T> InvokeAsync<T>(string id, CancellationToken ct, object?[]? args) => InvokeAsync<T>(id, args);
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
  }
}
