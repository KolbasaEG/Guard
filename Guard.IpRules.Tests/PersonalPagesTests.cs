using System.Reflection;
using System.Net;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Radzen;
using PersonalPages = Guard.Components.Pages.Personals;

internal static class PersonalPagesTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var child = new CompositeFilterDescriptor { Property = "FirstName", FilterOperator = FilterOperator.Contains,
      FilterValue = "Ivan", Filters = [] };
    var children = new List<CompositeFilterDescriptor> { child };
    var group = new CompositeFilterDescriptor { LogicalFilterOperator = LogicalFilterOperator.Or, Filters = children };
    var snapshot = IndexHarness.Capture([group]);
    child.FilterValue = "Petr";
    children.Clear();
    var people = new[] {
      new Personal { FirstName = "Ivan", LastName = "One" },
      new Personal { FirstName = "Petr", LastName = "Two" }
    }.AsQueryable();
    check(PersonalQueryFilter.Apply(people, snapshot).Single().FirstName == "Ivan",
      "personal applied filter copies nested groups, values and empty leaf child collections");
    check(PersonalQueryFilter.Apply(people, IndexHarness.Capture([])).Count() == 2,
      "cleared personal filter restores all rows in the supplied scope");
    foreach (var invalid in new[] {
      new CompositeFilterDescriptor { Property = "User.PasswordHash", FilterOperator = FilterOperator.Equals, FilterValue = "test" },
      new CompositeFilterDescriptor { Property = "LastName", FilterOperator = FilterOperator.GreaterThan, FilterValue = "test" },
      new CompositeFilterDescriptor { Property = "LastName", FilterOperator = FilterOperator.Equals, FilterValue = 42 }
    })
    {
      try { IndexHarness.Capture([invalid]); check(false, "invalid personal filter rejected before load"); }
      catch (ArgumentException) { check(true, "invalid personal filter rejected before load"); }
    }
    var recursive = new CompositeFilterDescriptor();
    recursive.Filters = [recursive];
    try { IndexHarness.Capture([recursive]); check(false, "recursive personal filter bounded"); }
    catch (ArgumentException) { check(true, "recursive personal filter bounded"); }

    var lowId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    var highId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    var ties = new[] {
      new Personal { Id = highId, FirstName = "Same", LastName = "Same", Subdivision = new() { Name = "B" } },
      new Personal { Id = lowId, FirstName = "Same", LastName = "Same", Subdivision = new() { Name = "A" } }
    }.AsQueryable();
    check(PersonalListQuery.Sort(ties, "FirstName desc").First().Id == lowId,
      "personal sort uses unique key to stabilize equal values");
    check(PersonalListQuery.Sort(ties, "Subdivision.Name desc").First().Id == highId,
      "personal subdivision sort accepts entity field and direction");
    check(PersonalListQuery.Sort(ties, "SubdivisionName").First().Id == lowId,
      "existing personal subdivision sort alias remains compatible");
    foreach (var invalid in new[] { "User.PasswordHash", "FirstName sideways", "LastName asc, FirstName desc" })
    {
      try { PersonalListQuery.Sort(ties, invalid); check(false, "invalid personal sort rejected"); }
      catch (ArgumentException) { check(true, "invalid personal sort rejected"); }
    }
    var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options;
    await using (var db = new ApplicationDbContext(options))
    {
      var sql = PersonalListQuery.Sort(db.Personals.AsNoTracking(), "Subdivision.Name desc").Skip(20).Take(20)
        .Select(p => new PersonalListItemDto(p.Id, p.LastName, p.FirstName, p.MiddleName, p.FullName,
          p.Subdivision == null ? null : p.Subdivision.Name, p.Status, p.Version)).ToQueryString();
      check(sql.Contains("ORDER BY") && sql.Contains("DESC") && sql.Contains("LIMIT") && sql.Contains("OFFSET") &&
        !sql.Contains("CreatedBy") && !sql.Contains("PersonalNumber"),
        "personal list sorting and DTO pagination translate without full personal data");

      var factory = new Contexts(options);
      await using var provider = new ServiceCollection().BuildServiceProvider();
      var writes = new UnitOfWorkFactory(factory, new PermissionStub(), provider, NullLogger<UnitOfWork>.Instance);
      var service = new PersonalService(null!, null!, writes, NullLogger<PersonalService>.Instance,
        new PermissionStub(), null!, factory);
      foreach (var year in new[] { 1899, 2101 })
      {
        try { await service.CreateFromDtoAsync(new(new PersonalFieldsDto { LastName = "Test", FirstName = "User", EnlistmentYear = year }));
          check(false, "invalid enlistment year rejected by create service"); }
        catch (ArgumentException) { check(true, "invalid enlistment year rejected by create service before DB access"); }
      }
    }

    var proxy = DispatchProxy.Create<IPersonalService, ServiceProxy>();
    var modeProxy = DispatchProxy.Create<IPersonalService, ServiceProxy>();
    PersonalSearchRequest? requestedMode = null;
    ((ServiceProxy)modeProxy).Handle = (method, args) => {
      requestedMode = (PersonalSearchRequest)args[0]!;
      return Task.FromResult(new PersonalSearchResult([], 0));
    };
    using (var modePage = new IndexHarness(modeProxy))
    {
      modePage.SetAccess(new("actor", true, []));
      await modePage.Load();
      check(requestedMode!.AllSubdivisions && !requestedMode.OwnSubdivision, "Root first opening defaults to all-personnel list request");
      modePage.UseOwn(); await modePage.Load();
      check(!requestedMode!.AllSubdivisions && requestedMode.OwnSubdivision, "Root own mode retains own-subdivision list request");
      modePage.SetAccess(new("other", false, [Permissions.Personals.Read]));
      await modePage.Load();
      check(!requestedMode!.AllSubdivisions && requestedMode.OwnSubdivision, "non-Root list request stays restricted after access change");
    }
    var fake = (ServiceProxy)proxy;
    var pending = new List<TaskCompletionSource<PersonalSearchResult>>();
    var tokens = new List<CancellationToken>();
    fake.Handle = (method, args) =>
    {
      if (method.Name != nameof(IPersonalService.SearchAsync)) throw new NotSupportedException(method.Name);
      var completion = new TaskCompletionSource<PersonalSearchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
      pending.Add(completion);
      tokens.Add((CancellationToken)args[1]!);
      return completion.Task;
    };
    using (var page = new IndexHarness(proxy))
    {
      var first = page.Load();
      var second = page.Load();
      check(tokens[0].IsCancellationRequested, "new personal load cancels previous request");
      pending[1].SetResult(new([new(lowId, "New", "User", null, null, null, Status.Inserted)], 1));
      await second;
      pending[0].SetResult(new([new(highId, "Old", "User", null, null, null, Status.Inserted)], 20));
      await first;
      check(page.Rows.Single().LastName == "New" && page.Count == 1 && !page.Busy,
        "late personal result cannot replace current page or its loading state");
      var third = page.Load();
      pending[2].SetException(new UnauthorizedAccessException());
      await third;
      check(!page.Rows.Any() && page.Count == 0 && page.Error != null,
        "failed personal load clears untrusted results and explains error");
      var fourth = page.Load();
      page.Dispose();
      check(tokens[3].IsCancellationRequested, "disposing personal list cancels running query");
      pending[3].SetResult(new([], 0));
      await fourth;
      check(!page.Rows.Any(), "disposed personal list ignores late result");
    }

    var createCalls = 0;
    fake.Handle = (method, _) => method.Name switch
    {
      nameof(IPersonalService.GetAllActiveSubdivisionsAsync) => Task.FromException<IReadOnlyList<Subdivision>>(new IOException()),
      nameof(IPersonalService.CreateFromDtoAsync) => CountCreate(),
      _ => throw new NotSupportedException(method.Name)
    };
    Task<Guid> CountCreate() { createCalls++; return Task.FromResult(lowId); }
    using (var add = new AddHarness(proxy))
    {
      await add.Initialize();
      await add.Save();
      check(!add.Loaded && add.Error != null && createCalls == 0,
        "failed personal form initialization cannot submit default entity");
    }
    foreach (var status in new[] { Status.Blocked, Status.Archived, Status.ArchivedBlocked, Status.Deleted })
    {
      fake.Handle = (method, _) => method.Name == nameof(IPersonalService.GetByIdAsync)
        ? Task.FromResult<Personal?>(new() { Id = lowId, Status = status })
        : throw new Exception("Inactive employee must not load editable references or save");
      using var edit = new EditHarness(proxy, lowId);
      await edit.Initialize();
      await edit.Save();
      check(!edit.Loaded && edit.Error != null, $"personal edit refuses {status} before offering save");
    }
    var original = new Personal { Id = lowId, LastName = "Original", FirstName = "User", Version = lowId };
    fake.Handle = (method, _) => method.Name switch
    {
      nameof(IPersonalService.GetByIdAsync) => Task.FromResult<Personal?>(original),
      nameof(IPersonalService.GetAllActiveSubdivisionsAsync) => Task.FromResult<IReadOnlyList<Subdivision>>([]),
      _ => throw new NotSupportedException(method.Name)
    };
    using (var edit = new EditHarness(proxy, lowId))
    {
      await edit.Initialize();
      edit.ChangeName("Entered");
      edit.Refresh(new() { Id = lowId, LastName = "Original", FirstName = "User", Version = highId, Status = Status.Modified });
      check(edit.Name == "Entered" && edit.Version == highId,
        "successful nested IP edit refreshes version without discarding entered personal fields");
      try { edit.Refresh(new() { Id = lowId, LastName = "Other editor", FirstName = "User", Version = Guid.NewGuid() });
        check(false, "nested IP refresh must preserve concurrent field conflict"); }
      catch (EntityConflictException)
      {
        check(edit.Name == "Entered" && edit.Version == highId,
          "nested IP refresh does not accept version covering another editor's field changes");
      }
    }
    fake.Handle = (method, _) => method.Name == nameof(IPersonalService.GetDetailsAsync)
      ? Task.FromResult<PersonalDetailsDto?>(null) : throw new NotSupportedException();
    using (var details = new DetailsHarness(proxy, lowId))
    {
      await details.Initialize();
      check(details.Error != null && !details.Busy, "unavailable personal card remains with readable error");
    }
    check(Enum.GetValues<Status>().All(status => !PersonalPages.Details.StatusName(status).Contains(status.ToString())),
      "personal lifecycle names are localized");
    fake.Handle = (method, _) => method.Name switch
    {
      nameof(IPersonalService.GetDetailsAsync) => Task.FromResult<PersonalDetailsDto?>(new()
      {
        Id = lowId, FirstName = "Иван", LastName = "Тестовый", Status = Status.Blocked,
        PersonalNumber = "card-123", CreatedBy = "audit-only", UserName = "account-test", IpAddresses = ["192.0.2.1"]
      }),
      _ => throw new NotSupportedException(method.Name)
    };
    await using var renderServices = new ServiceCollection().AddLogging()
      .AddSingleton<IDirectoryManagementService, LocalManagement>()
      .AddSingleton(proxy).AddSingleton<NotificationService>().AddScoped<DialogService>()
      .AddSingleton<IJSRuntime, NoJs>().AddSingleton<NavigationManager, TestNavigation>().BuildServiceProvider();
    await using var renderer = new HtmlRenderer(renderServices, renderServices.GetRequiredService<ILoggerFactory>());
    await renderer.Dispatcher.InvokeAsync(async () =>
    {
      var root = await renderer.RenderComponentAsync<CardHost>(ParameterView.FromDictionary(new Dictionary<string, object?>
      {
        [nameof(CardHost.Id)] = lowId,
        [nameof(CardHost.AllowDetails)] = true
      }));
      var html = WebUtility.HtmlDecode(root.ToHtmlString());
      check(html.Contains("Основные данные", StringComparison.OrdinalIgnoreCase) && html.Contains("Служба и подразделение", StringComparison.OrdinalIgnoreCase) &&
        html.Contains("Заблокирован") && html.Contains("card-123") && html.Contains("192.0.2.1"),
        "actual personal card renders grouped fields, IPs and localized status");
      check(!html.Contains("audit-only") && !html.Contains(lowId.ToString()),
        "personal card keeps audit and technical identifiers out of main view");
      var denied = await renderer.RenderComponentAsync<CardHost>(ParameterView.FromDictionary(new Dictionary<string, object?>
      {
        [nameof(CardHost.Id)] = lowId,
        [nameof(CardHost.AllowDetails)] = false
      }));
      check(!WebUtility.HtmlDecode(denied.ToHtmlString()).Contains("card-123"),
        "personal card hides details when read-details permission is absent");
    });
  }

  public class ServiceProxy : DispatchProxy
  {
    public Func<MethodInfo, object?[], object?> Handle { get; set; } = default!;
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handle(targetMethod!, args ?? []);
  }

  private sealed class LocalManagement : IDirectoryManagementService
  {
    public Task<bool> IsApiManagedAsync(CancellationToken ct = default) => Task.FromResult(false);
  }

  public sealed class CardHost : ComponentBase
  {
    [Parameter] public Guid Id { get; set; }
    [Parameter] public bool AllowDetails { get; set; }
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
      builder.OpenComponent<CascadingValue<UserAccessSnapshot>>(0);
      builder.AddAttribute(1, "Value", new UserAccessSnapshot("actor", false,
        AllowDetails ? [Permissions.Personals.Read, Permissions.Personals.ReadDetails] : [Permissions.Personals.Read]));
      builder.AddAttribute(2, "ChildContent", (RenderFragment)(child =>
      {
        child.OpenComponent<PersonalPages.Details>(0);
        child.AddAttribute(1, nameof(PersonalPages.Details.Id), Id);
        child.CloseComponent();
      }));
      builder.CloseComponent();
    }
  }

  private sealed class NoJs : IJSRuntime
  {
    public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => ValueTask.FromResult(default(T)!);
    public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken ct, object?[]? args) => InvokeAsync<T>(identifier, args);
  }

  private sealed class TestNavigation : NavigationManager
  {
    public TestNavigation() => Initialize("http://localhost/", "http://localhost/personal");
    protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
  }

  private sealed class IndexHarness : PersonalPages.Index
  {
    public IndexHarness(IPersonalService service)
    {
      Service = service;
      PermissionService = new PermissionStub();
      Logger = NullLogger<PersonalPages.Index>.Instance;
      Notifications = new();
      Access = new("actor", true, []);
    }
    public static PersonalFilter Capture(IEnumerable<CompositeFilterDescriptor> conditions) => CaptureFilter(conditions, LogicalFilterOperator.And);
    public void SetAccess(UserAccessSnapshot access) { Access = access; OnParametersSet(); }
    public void UseOwn() => allRecords = false;
    public Task Load() => LoadData(new() { Skip = 0, Top = 20 });
    public IEnumerable<PersonalListItemDto> Rows => filteredData;
    public int Count => count;
    public bool Busy => isLoading;
    public string? Error => error;
  }

  private sealed class AddHarness : PersonalPages.Add
  {
    public AddHarness(IPersonalService service)
    {
      PersonalService = service;
      Logger = NullLogger<PersonalPages.Add>.Instance;
      NotificationService = new();
      Access = new("actor", true, []);
    }
    public Task Initialize() => OnParametersSetAsync();
    public Task Save() => FormSubmit();
    public bool Loaded => loaded;
    public string? Error => error;
  }

  private sealed class EditHarness : PersonalPages.Edit
  {
    public EditHarness(IPersonalService service, Guid id)
    {
      Service = service;
      Permissions = new PermissionStub();
      Logger = NullLogger<PersonalPages.Edit>.Instance;
      Notifications = new();
      Id = id;
      Access = new("actor", true, []);
    }
    public Task Initialize() => OnParametersSetAsync();
    public Task Save() => FormSubmit();
    public bool Loaded => loaded;
    public string? Error => error;
    public void ChangeName(string name) => item.FirstName = name;
    public string Name => item.FirstName;
    public Guid Version => item.Version;
    public void Refresh(Personal current) => RefreshAfterIpSave(current);
  }

  private sealed class DetailsHarness : PersonalPages.Details
  {
    public DetailsHarness(IPersonalService service, Guid id)
    {
      Service = service;
      Logger = NullLogger<PersonalPages.Details>.Instance;
      Notifications = new();
      Id = id;
    }
    public Task Initialize() => OnParametersSetAsync();
    public bool Busy => isLoading;
    public string? Error => error;
  }

  private sealed class Contexts(DbContextOptions<ApplicationDbContext> options) : IDbContextFactory<ApplicationDbContext>
  {
    public ApplicationDbContext CreateDbContext() => new(options);
  }

  private sealed class PermissionStub : IPermissionService
  {
    public Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) => Task.FromResult(new UserAccessSnapshot("actor", true, []));
    public Task<UserAccessSnapshot> GetForUserAsync(string id, CancellationToken ct = default) => GetCurrentAsync(ct);
    public Task<bool> HasAsync(string code, CancellationToken ct = default) => Task.FromResult(true);
    public Task RequireAsync(string code, CancellationToken ct = default) => Task.CompletedTask;
  }
}
