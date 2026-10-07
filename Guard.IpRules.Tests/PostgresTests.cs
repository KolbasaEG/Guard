using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using System.Security.Claims;
using System.Text.Json;

internal static class PostgresTests
{
  public static async Task RunAsync(string settingsPath, Action<bool, string> check)
  {
    using var json = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath));
    var cs = new NpgsqlConnectionStringBuilder(json.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString());
    if (cs.Host is not ("localhost" or "127.0.0.1" or "::1"))
      throw new InvalidOperationException("Integration checks only create a temporary database on localhost.");
    var name = "guard_ip_test_" + Guid.NewGuid().ToString("N");
    cs.Database = "postgres";
    await using var admin = new NpgsqlConnection(cs.ConnectionString);
    await admin.OpenAsync();
    await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin)) await create.ExecuteNonQueryAsync();
    cs.Database = name;
    var factory = new Factory(cs.ConnectionString);
    try
    {
      await using var db = factory.CreateDbContext();
      await db.Database.EnsureCreatedAsync();
      var own = new Subdivision { Name = "Own", Path = "/1/", CreatedBy = "test" };
      var other = new Subdivision { Name = "Other", Path = "/2/", CreatedBy = "test" };
      var personal = new Personal { FirstName = "Test", LastName = "User", Subdivision = own, CreatedBy = "test" };
      var foreignPersonal = new Personal { FirstName = "Other", LastName = "User", Subdivision = other, CreatedBy = "test" };
      var user = new ApplicationUser { Id = "user", UserName = "user", NormalizedUserName = "USER", Personal = personal };
      var root = new ApplicationUser { Id = "root", UserName = "root", NormalizedUserName = "ROOT" };
      var role = new ApplicationRole { Id = "root-role", Name = "Root", NormalizedName = "ROOT" };
      var adminRole = new ApplicationRole { Id = "admin-role", Name = "Администратор", NormalizedName = "АДМИНИСТРАТОР" };
      db.AddRange(own, other, personal, foreignPersonal, user, root, role, adminRole);
      db.UserRoles.Add(new() { UserId = root.Id, RoleId = role.Id });
      db.UserRoles.Add(new() { UserId = user.Id, RoleId = adminRole.Id });
      foreach (var permission in PermissionCatalog.All.Where(p => !p.RootOnly)) db.RoleClaims.Add(new() { RoleId = adminRole.Id, ClaimType = "Permission", ClaimValue = permission.Code });
      await db.SaveChangesAsync();

      var gate = new IpAccessService(factory);
      check(!await gate.IsAllowedAsync("missing", "192.168.1.1"), "unknown user denied");
      check(!await gate.IsAllowedAsync(user.Id, "192.168.1.1"), "empty assignments denied");
      check(await gate.IsAllowedAsync(root.Id, null), "Root bypasses IP without Personal");

      var rootPermissions = new PermissionService(new Authentication(root.Id), new Microsoft.AspNetCore.Http.HttpContextAccessor(), factory, new AuditStub());
      var userPermissions = new PermissionService(new Authentication(user.Id), new Microsoft.AspNetCore.Http.HttpContextAccessor(), factory, new AuditStub());
      var rootAccess = new IpManagementAccessService(rootPermissions, new DataAccessScopeService(rootPermissions, factory));
      var userAccess = new IpManagementAccessService(userPermissions, new DataAccessScopeService(userPermissions, factory));
      var rootService = new IpAddressService(factory, rootAccess, NullLogger<IpAddressService>.Instance);
      var service = new IpAddressService(factory, userAccess, NullLogger<IpAddressService>.Instance);
      var assignments = new PersonalIpService(factory, userAccess, NullLogger<PersonalIpService>.Instance);
      var id = await service.CreateAsync(new IpAddress { Address = "192.168.1.99/24", SubdivisionId = own.Id });
      check((await service.GetByIdAsync(id))!.Address == "192.168.1.0/24", "catalog stores canonical subnet");
      check(!await gate.IsAllowedAsync(user.Id, "192.168.1.1"), "subdivision address alone does not grant access");
      await assignments.UpdateAsync(personal.Id, [id]);
      check(await gate.IsAllowedAsync(user.Id, "::ffff:192.168.1.1"), "assigned mapped client matches subnet");
      check(!await gate.IsAllowedAsync(user.Id, "192.168.2.1"), "outside subnet denied");
      check((await assignments.GetAsync(personal.Id)).Selected.SequenceEqual([id]), "assignments saved");
      await service.ArchiveAsync(id);
      check(!await gate.IsAllowedAsync(user.Id, "192.168.1.1"), "archived assigned address denied");
      check((await service.GetAllActiveAsync()).Count == 0, "active list excludes archive");
      await service.RestoreAsync(id);
      await service.BlockAsync(id);
      check(!await gate.IsAllowedAsync(user.Id, "192.168.1.1"), "blocked assigned address denied");
      await service.UnblockAsync(id);
      check(await gate.IsAllowedAsync(user.Id, "192.168.1.1"), "unblocked assignment restores access");
      await service.SoftDeleteAsync(id);
      check(!await gate.IsAllowedAsync(user.Id, "192.168.1.1"), "deleted assigned address denied");
      await service.RestoreAsync(id);

      await ThrowsAsync<ArgumentException>(() => service.CreateAsync(new IpAddress { Address = "192.168.1.2/24", SubdivisionId = own.Id }), check, "equivalent duplicate rejected");
      await ThrowsAsync<ArgumentException>(() => service.CreateAsync(new IpAddress { Address = "bad", SubdivisionId = own.Id }), check, "server rejects invalid address");
      await ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync(new IpAddress { Address = "1.2.3.4", SubdivisionId = other.Id }), check, "foreign subdivision write denied");
      await ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync(new IpAddress { Address = "1.2.3.4" }), check, "non-Root common IP write denied");
      await ThrowsAsync<UnauthorizedAccessException>(() => assignments.UpdateAsync(foreignPersonal.Id, [id]), check, "foreign employee assignment denied");
      await ThrowsAsync<InvalidOperationException>(() => assignments.UpdateAsync(personal.Id, [Guid.NewGuid()]), check, "invalid assignment rejected atomically");
      check(await gate.IsAllowedAsync(user.Id, "192.168.1.1"), "rejected assignment preserves original");
      var common = await rootService.CreateAsync(new IpAddress { Address = "2001:db8::1/64" });
      check(await service.GetByIdAsync(common) == null, "common address hidden from non-Root");
      check((await rootService.GetAllActiveAsync()).Count == 2, "Root sees common addresses");
      await ThrowsAsync<InvalidOperationException>(() => assignments.UpdateAsync(personal.Id, [common]), check, "non-Root cannot assign inaccessible common address");

      var rootAssignments = new PersonalIpService(factory, rootAccess, NullLogger<PersonalIpService>.Instance);
      await rootAssignments.UpdateAsync(personal.Id, [common]);
      check(await gate.IsAllowedAsync(user.Id, "2001:db8::42"), "IPv6 assigned subnet works");
      check(!await gate.IsAllowedAsync(user.Id, "192.168.1.1"), "assignment replacement removes old permission");
      await rootAssignments.UpdateAsync(personal.Id, []);
      check(!await gate.IsAllowedAsync(user.Id, "2001:db8::42"), "clearing assignments denies next connection");

      // Одновременные вставки нормализуются к одному адресу; ровно одна должна пройти.
      var concurrent = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
      {
        try { await service.CreateAsync(new IpAddress { Address = "10.20.30.99/24", SubdivisionId = own.Id }); return true; }
        catch (ArgumentException) { return false; }
      }));
      check(concurrent.Count(v => v) == 1, "concurrent duplicate insertion has one winner");
      await SignInTests.RunAsync(factory, gate, check);
      await AuthorizationTests.RunAsync(factory, check);
      Console.WriteLine("PostgreSQL integration checks passed in an isolated temporary database.");
    }
    finally
    {
      NpgsqlConnection.ClearAllPools();
      // Только созданная этим запуском БД с фиксированным префиксом и UUID.
      await using var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", admin);
      await drop.ExecuteNonQueryAsync();
    }
  }

  internal static async Task ThrowsAsync<T>(Func<Task> action, Action<bool, string> check, string scenario) where T : Exception
  {
    try { await action(); check(false, scenario); }
    catch (T) { check(true, scenario); }
  }

  internal sealed class Factory(string connection) : IDbContextFactory<ApplicationDbContext>
  {
    public ApplicationDbContext CreateDbContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
    public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken ct = default) => Task.FromResult(CreateDbContext());
  }

  private sealed class Authentication(string userId) : AuthenticationStateProvider
  {
    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(
        new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test"))));
  }
}
