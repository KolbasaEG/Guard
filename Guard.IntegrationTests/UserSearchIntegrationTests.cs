using System.Text.Json;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Guard.Core.Services.Exports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class UserSearchIntegrationTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory, Action<bool, string> check)
  {
    await using var provider = new ServiceCollection().BuildServiceProvider();
    var permissions = new PermissionsStub();
    var scopes = new ScopeStub();
    var policy = new PolicyStub();
    var reads = new ReadRepository<ApplicationUser>(factory, permissions, scopes);
    var service = new SecurityService(reads, permissions, scopes,
      provider.GetRequiredService<IServiceScopeFactory>(), null!, null!, policy, Microsoft.Extensions.Logging.Abstractions.NullLogger<SecurityService>.Instance);
    await using var db = await factory.CreateDbContextAsync();
    var own = await db.Subdivisions.SingleAsync(s => s.Name == "Own");
    var other = await db.Subdivisions.SingleAsync(s => s.Name == "Other");
    var p1 = new Personal { FirstName = "Search", LastName = "Own", SubdivisionId = own.Id, CreatedBy = "test" };
    var p2 = new Personal { FirstName = "Search", LastName = "Other", SubdivisionId = other.Id, CreatedBy = "test" };
    var now = DateTimeOffset.UtcNow;
    var a = new ApplicationUser { UserName = "search-alpha", Email = "same@test", Personal = p1, LastActivityAtUtc = now,
      PasswordChangedAtUtc = now.AddDays(-40), AccountBlockReason = "Administrator" };
    var b = new ApplicationUser { UserName = "search-beta", Email = "same@test", Personal = p2, MustChangePassword = true };
    var c = new ApplicationUser { UserName = "search-null", Email = null, LockoutEnabled = true, LockoutEnd = now.AddHours(1) };
    db.AddRange(p1, p2, a, b, c); await db.SaveChangesAsync();
    ExportFilter Text(string field, string op, object value) => new(field, op, JsonSerializer.SerializeToElement(value));
    var all = Text("UserName", "StartsWith", "SEARCH-");
    try {
      var result = await service.SearchUsersAsync(new(Take: 1, OrderBy: "Email desc", Filter: all));
      var next = await service.SearchUsersAsync(new(Skip: 1, Take: 1, OrderBy: "Email desc", Filter: all));
      check(result.Count == 3 && result.Items.Count == 1 && result.Items[0].Id != next.Items.Single().Id, "user search counts before pagination with stable ties");
      foreach (var field in UserSearchQuery.Fields)
        check((await service.SearchUsersAsync(new(OrderBy: field + " desc", Filter: all))).Count == 3, "user sort translates to SQL: " + field);
      var items = (await service.SearchUsersAsync(new(Filter: all))).Items;
      check(items.Single(u => u.Id == a.Id).PasswordChangeRequired && items.Single(u => u.Id == a.Id).IsLockedOut &&
        items.Single(u => u.Id == b.Id).MustChangePassword && items.Single(u => u.Id == c.Id).IsLockedOut &&
        items.Single(u => u.Id == c.Id).Email == "-", "user DTO computes policy, permanent and temporary block, and null display");
      foreach (var filter in new[] { Text("PersonalId", "Equals", p1.Id), Text("LastActivityAtUtc", "GreaterThan", now.AddMinutes(-1)),
        Text("MustChangePassword", "Equals", true) })
        check((await service.SearchUsersAsync(new(Filter: new(Children: [all, filter])))).Count == 1, "typed nullable Guid/date/bool user filter executes in SQL");
      check((await service.SearchUsersAsync(new(Filter: new(Children: [all, new("PersonalId", "IsNull")])))).Count == 1, "user nullable filter finds unbound accounts for Root");
      permissions.Actor = new("reader", false, [Permissions.Users.Read]);
      scopes.Value = new("reader", false, [own.Id]);
      check((await service.SearchUsersAsync(new(Filter: all))).Items.Single().Id == a.Id, "ordinary user search is restricted to server subdivision");
      scopes.Value = new("reader", false, []);
      check((await service.SearchUsersAsync(new(Filter: all))).Count == 0, "empty user scope denies all rows");
      permissions.Actor = new("reader", false, []);
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.SearchUsersAsync(new()), check, "user search requires Users.Read");
      permissions.Actor = new("reader", false, [Permissions.Users.Read]);
      scopes.Value = new("reader", false, [own.Id]); scopes.Calls = 0; scopes.ChangeAfter = 2;
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.SearchUsersAsync(new()), check, "scope change during user search discards result");
      scopes.ChangeAfter = int.MaxValue; scopes.Calls = 0;
      permissions.Calls = 0; permissions.ChangeAfter = 2;
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.SearchUsersAsync(new()), check, "actor change during user search discards result");
      permissions.ChangeAfter = int.MaxValue;
      foreach (var request in new[] { new UserSearchRequest(Skip: -1), new(Take: 101), new(OrderBy: "PasswordHash asc"),
        new(Filter: Text("SecurityStamp", "Equals", "secret")), new(Filter: Text("MustChangePassword", "Contains", true)),
        new(Filter: Text("LastActivityAtUtc", "Equals", now.ToOffset(TimeSpan.FromHours(3)))) })
        await PostgresTests.ThrowsAsync<ArgumentException>(() => service.SearchUsersAsync(request), check, "user search rejects unsafe field, page, operation or non-UTC date");
      using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
      await PostgresTests.ThrowsAsync<OperationCanceledException>(() => service.SearchUsersAsync(new(), cancelled.Token), check, "user search honors cancellation");
    }
    finally {
      await db.Users.Where(u => u.Id == a.Id || u.Id == b.Id || u.Id == c.Id).ExecuteDeleteAsync();
      await db.Personals.Where(p => p.Id == p1.Id || p.Id == p2.Id).ExecuteDeleteAsync();
    }
  }
  private sealed class PermissionsStub : IPermissionService
  {
    public UserAccessSnapshot Actor = new("reader", true, []);
    public int Calls, ChangeAfter = int.MaxValue;
    public Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) => Task.FromResult(++Calls > ChangeAfter ? Actor with { UserId = "changed" } : Actor);
    public Task<UserAccessSnapshot> GetForUserAsync(string id, CancellationToken ct = default) => GetCurrentAsync(ct);
    public Task<bool> HasAsync(string permission, CancellationToken ct = default) => Task.FromResult(Actor.Has(permission));
    public Task RequireAsync(string permission, CancellationToken ct = default) => Actor.Has(permission) ? Task.CompletedTask : throw new UnauthorizedAccessException();
  }
  private sealed class ScopeStub : IDataAccessScopeService
  {
    public DataAccessScope Value = new("reader", true, []);
    public int Calls, ChangeAfter = int.MaxValue;
    public Task<DataAccessScope> GetAsync(CancellationToken ct = default) => Task.FromResult(++Calls > ChangeAfter ? Value with { SubdivisionIds = [] } : Value);
  }
  private sealed class PolicyStub : IAccountPolicyService
  {
    public Task<AccountPasswordLifetimeDto> GetPasswordLifetimeAsync(CancellationToken ct = default) => Task.FromResult(new AccountPasswordLifetimeDto(true, 30));
    public Task<AccountLoginPolicyDto> GetLoginPolicyAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<AccountState> CheckAsync(string id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task RecordActivityAsync(string id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task SweepAsync(CancellationToken ct = default) => throw new NotSupportedException();
  }
}
