using System.Text.Json;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Guard.Core.Services.Exports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

internal static class AdministrationSearchIntegrationTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory, Action<bool, string> check)
  {
    var permissions = new PermissionsStub();
    var roles = new ApplicationRoleService(new ReadRepository<ApplicationRole>(factory, permissions, null!),
      permissions);
    await using var db = await factory.CreateDbContextAsync();
    var entries = Enumerable.Range(1, 24).Select(n => new ApplicationRole { Name = $"search-role-{n:00}", NormalizedName = $"SEARCH-ROLE-{n:00}" }).ToArray();
    db.Roles.AddRange(entries); await db.SaveChangesAsync();
    // This database is created and dropped by PostgresTests. No schema changes run against application databases.
    await db.Database.ExecuteSqlRawAsync("CREATE TABLE logs (id uuid PRIMARY KEY DEFAULT gen_random_uuid(), timestamp timestamp without time zone, level text, layer text, message text, message_template text, properties jsonb, exception text, user_id text, ip_address text)");
    await db.Database.ExecuteSqlRawAsync("INSERT INTO logs (timestamp, level, layer, message, exception, user_id, ip_address) SELECT timestamp '2026-01-01', CASE WHEN n % 2 = 0 THEN 'Error' ELSE 'Information' END, 'Service', 'search-log-' || lpad(n::text, 2, '0'), NULL, 'test', NULL FROM generate_series(1, 24) n");
    var logs = new LogService(new LogFactory(db.Database.GetConnectionString()!), permissions);
    ExportFilter Text(string field, string op, object value) => new(field, op, JsonSerializer.SerializeToElement(value));
    var roleFilter = Text("Name", "StartsWith", "SEARCH-ROLE-");
    try {
      var first = await roles.SearchAsync(new(Take: 10, Filter: roleFilter));
      var second = await roles.SearchAsync(new(Skip: 10, Take: 10, Filter: roleFilter));
      check(first.Count == 24 && first.Items.Count == 10 && second.Items.Count == 10 && !first.Items.Select(r => r.Id).Intersect(second.Items.Select(r => r.Id)).Any(), "role search counts before pagination");
      foreach (var field in RoleSearchQuery.Fields)
        check((await roles.SearchAsync(new(OrderBy: field + " desc", Filter: roleFilter))).Count == 24, "role sort executes in SQL: " + field);
      check((await roles.SearchAsync(new(Filter: new(Children: [roleFilter, Text("Name", "EndsWith", "01")])))).Count == 1, "role filter groups execute in SQL");
      var page1 = await logs.SearchAsync(new(Take: 10));
      var page2 = await logs.SearchAsync(new(Skip: 10, Take: 10));
      check(page1.Count == 24 && page1.Items.Count == 10 && !page1.Items.Select(l => l.Message).Intersect(page2.Items.Select(l => l.Message)).Any(), "keyless logs resolve equal timestamp ties by visible values");
      check(page1.Items.All(l => l.Timestamp.Kind == DateTimeKind.Utc && l.Exception == null), "log DTO preserves UTC and nullable fields");
      foreach (var field in LogSearchQuery.Fields)
        check((await logs.SearchAsync(new(OrderBy: field + " desc"))).Count == 24, "log sort executes in SQL: " + field);
      check((await logs.SearchAsync(new(Filter: Text("Level", "In", new[] { "Error" })))).Count == 12, "log level selection executes in SQL");
      check((await logs.SearchAsync(new(Filter: Text("Level", "NotIn", new[] { "Error" })))).Count == 12, "log negative selection executes in SQL");
      check((await logs.SearchAsync(new(Filter: Text("Timestamp", "GreaterThanOrEquals", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))))).Count == 24, "log UTC boundary executes in SQL");
      check((await logs.SearchAsync(new(Filter: new("Exception", "IsNull")))).Count == 24, "log null filter executes in SQL");
      await db.Database.ExecuteSqlRawAsync("INSERT INTO logs (timestamp, level, layer, message, exception, user_id, ip_address) SELECT timestamp, level, layer, message, exception, user_id, ip_address FROM logs WHERE message = 'search-log-01'");
      var duplicates = await logs.SearchAsync(new(Filter: Text("Message", "Equals", "search-log-01")));
      check(duplicates.Count == 2 && duplicates.Items.Count == 2 && !Equals(duplicates.Items[0], duplicates.Items[1]),
        "identical log events remain distinct Radzen row keys");
      foreach (var input in new[] { new RoleSearchRequest(Skip: -1), new(Take: 101), new(OrderBy: "NormalizedName"), new(Filter: Text("ConcurrencyStamp", "Equals", "x")) })
        await PostgresTests.ThrowsAsync<ArgumentException>(() => roles.SearchAsync(input), check, "role search rejects unsupported request");
      foreach (var input in new[] { new LogSearchRequest(Skip: -1), new(Take: 0), new(OrderBy: "Timestamp.ToString()"), new(OrderBy: "Timestamp sideways"),
        new(Filter: Text("Timestamp", "Contains", "x")), new(Filter: Text("Level", "Equals", 1)),
        new(Filter: Text("Timestamp", "Equals", DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified))),
        new(Filter: new(Children: Enumerable.Repeat(new ExportFilter("Level", "IsNull"), 101).ToArray())) })
        await PostgresTests.ThrowsAsync<ArgumentException>(() => logs.SearchAsync(input), check, "log search rejects unsupported request");
      permissions.Actor = new("reader", false, [Permissions.Logs.Read]);
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => logs.SearchAsync(new()), check, "technical logs remain Root-only even with individual permission");
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => roles.SearchAsync(new()), check, "ordinary log reader cannot read Root-only roles");
      permissions.Actor = new("reader", false, []);
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => logs.SearchAsync(new()), check, "log search requires Logs.Read");
      permissions.Actor = new("reader", true, []); permissions.Calls = 0; permissions.ChangeAfter = 1;
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => roles.SearchAsync(new()), check, "role search discards result after actor change");
      permissions.Calls = 0;
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => logs.SearchAsync(new()), check, "log search discards result after actor change");
      permissions.Calls = 0; permissions.RevokeRoot = true;
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => roles.SearchAsync(new()), check, "role search rejects Root revocation during query");
      permissions.Calls = 0;
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => logs.SearchAsync(new()), check, "log search rejects Root revocation during query");
      permissions.ChangeAfter = int.MaxValue;
      using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
      await PostgresTests.ThrowsAsync<OperationCanceledException>(() => roles.SearchAsync(new(), cancelled.Token), check, "role search honors cancellation");
      await PostgresTests.ThrowsAsync<OperationCanceledException>(() => logs.SearchAsync(new(), cancelled.Token), check, "log search honors cancellation");
    }
    finally {
      var ids = entries.Select(r => r.Id).ToArray();
      await db.Roles.Where(r => ids.Contains(r.Id)).ExecuteDeleteAsync();
      await db.Database.ExecuteSqlRawAsync("DROP TABLE logs");
    }
  }

  private sealed class LogFactory(string connection) : IDbContextFactory<LogsDbContext>
  {
    public LogsDbContext CreateDbContext() => new(new DbContextOptionsBuilder<LogsDbContext>().UseNpgsql(connection).Options);
  }

  internal sealed class PermissionsStub : IPermissionService
  {
    public UserAccessSnapshot Actor = new("reader", true, []);
    public int Calls, ChangeAfter = int.MaxValue;
    public bool RevokeRoot;
    public Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) => Task.FromResult(++Calls > ChangeAfter
      ? RevokeRoot ? Actor with { IsRoot = false } : Actor with { UserId = "changed" } : Actor);
    public Task<UserAccessSnapshot> GetForUserAsync(string id, CancellationToken ct = default) => GetCurrentAsync(ct);
    public Task<bool> HasAsync(string permission, CancellationToken ct = default) => Task.FromResult(Actor.Has(permission));
    public Task RequireAsync(string permission, CancellationToken ct = default) => Actor.Has(permission) ? Task.CompletedTask : throw new UnauthorizedAccessException();
  }
}
