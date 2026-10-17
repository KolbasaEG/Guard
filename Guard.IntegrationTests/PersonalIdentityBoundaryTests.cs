using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

internal static class PersonalIdentityBoundaryTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory, Action<bool, string> check)
  {
    await using (var db = await factory.CreateDbContextAsync())
    {
      check(typeof(Personal).GetProperty("User") == null, "employee has no inverse Identity navigation");
      var employee = db.Model.FindEntityType(typeof(Personal))!;
      var user = db.Model.FindEntityType(typeof(ApplicationUser))!;
      var fk = user.GetForeignKeys().Single(f => f.PrincipalEntityType == employee);
      check(fk.IsUnique && !fk.IsRequired && fk.DeleteBehavior == DeleteBehavior.SetNull &&
        fk.Properties.Single().Name == nameof(ApplicationUser.PersonalId) &&
        fk.DependentToPrincipal?.Name == nameof(ApplicationUser.Personal) && fk.PrincipalToDependent == null,
        "Identity keeps optional unique employee FK, forward navigation and SetNull");
      check(!db.Database.HasPendingModelChanges(), "removing inverse navigation produces no relational differences against migration snapshot");
    }

    var prefix = "personal-identity-" + Guid.NewGuid().ToString("N");
    var own = new Subdivision { Name = prefix, Path = "/900004/", CreatedBy = "test" };
    var foreign = new Subdivision { Name = prefix + "-foreign", Path = "/900005/", CreatedBy = "test" };
    var employeeRow = new Personal { FirstName = "Identity", LastName = "Boundary", SubdivisionId = own.Id, CreatedBy = "test" };
    var otherEmployee = new Personal { FirstName = "Foreign", LastName = "Boundary", SubdivisionId = foreign.Id, CreatedBy = "test" };
    var account = new ApplicationUser { Id = prefix, UserName = prefix, PersonalId = employeeRow.Id };
    var unbound = new ApplicationUser { Id = prefix + "-unbound", UserName = prefix + "-unbound" };
    await using (var db = await factory.CreateDbContextAsync())
    {
      db.AddRange(own, foreign, employeeRow, otherEmployee, account, unbound);
      await db.SaveChangesAsync();
    }
    try
    {
      var access = new Access();
      var scopes = new Scope(own.Id, access);
      var service = new PersonalService(new ReadRepository<Personal>(factory, access, scopes), null!,
        NullLogger<PersonalService>.Instance, access, scopes, factory);
      var card = await service.GetDetailsAsync(employeeRow.Id);
      check(card?.UserId == account.Id && card.UserName == account.UserName && card.FirstName == employeeRow.FirstName,
        "protected employee card projects linked account without inverse navigation");
      check(await service.GetDetailsAsync(otherEmployee.Id) == null, "employee card hides foreign employee before reading linked account");
      check(await service.GetDetailsAsync(Guid.NewGuid()) == null, "employee card returns null for missing employee");
      access.Snapshot = new("boundary", false, [Permissions.Personals.Read]);
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.GetDetailsAsync(employeeRow.Id), check,
        "linked account does not bypass employee details permission");
      access.Snapshot = new("boundary", false, [Permissions.Personals.Read, Permissions.Personals.ReadDetails]);
      foreach (var status in Enum.GetValues<Status>())
      {
        await using (var db = await factory.CreateDbContextAsync())
          await db.Personals.Where(p => p.Id == employeeRow.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, status));
        card = await service.GetDetailsAsync(employeeRow.Id);
        check((card != null) == (status is Status.Inserted or Status.Modified or Status.Blocked),
          $"employee card preserves non-Root visibility for {status}");
      }
      access.Snapshot = new("boundary", false, [Permissions.Personals.Read, Permissions.Personals.ReadDetails, Permissions.Personals.ReadArchive]);
      await using (var db = await factory.CreateDbContextAsync())
        await db.Personals.Where(p => p.Id == employeeRow.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, Status.ArchivedBlocked));
      check((await service.GetDetailsAsync(employeeRow.Id))?.UserId == account.Id, "archive permission preserves linked account projection");
      access.Snapshot = new("boundary", true, []);
      check(await service.GetDetailsAsync(otherEmployee.Id) is { UserId: null, UserName: null }, "Root can read foreign employee without linked account");

      await ConstraintAsync(factory, new ApplicationUser { Id = prefix + "-duplicate", PersonalId = employeeRow.Id },
        PostgresErrorCodes.UniqueViolation, check, "database rejects a second account for the same employee");
      await ConstraintAsync(factory, new ApplicationUser { Id = prefix + "-orphan", PersonalId = Guid.NewGuid() },
        PostgresErrorCodes.ForeignKeyViolation, check, "database rejects a nonexistent employee FK");
      await using (var db = await factory.CreateDbContextAsync())
      {
        db.Users.Add(new ApplicationUser { Id = prefix + "-another-unbound" });
        await db.SaveChangesAsync();
        check(await db.Users.CountAsync(u => u.Id.StartsWith(prefix) && u.PersonalId == null) == 2,
          "optional unique FK permits multiple unbound accounts");
        await db.Users.Where(u => u.Id == account.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.PersonalId, (Guid?)null));
      }
      check(await service.GetDetailsAsync(employeeRow.Id) is { UserId: null, UserName: null },
        "same service reloads removed employee-account binding");
      await using (var db = await factory.CreateDbContextAsync())
        await db.Users.Where(u => u.Id == unbound.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.PersonalId, employeeRow.Id));
      check((await service.GetDetailsAsync(employeeRow.Id))?.UserId == unbound.Id, "same service reloads replacement account binding");
      await using (var db = await factory.CreateDbContextAsync())
        await db.Personals.Where(p => p.Id == employeeRow.Id).ExecuteDeleteAsync();
      await using (var db = await factory.CreateDbContextAsync())
      {
        var saved = await db.Users.AsNoTracking().SingleAsync(u => u.Id == unbound.Id);
        check(saved.PersonalId == null && saved.UserName == unbound.UserName,
          "database SetNull preserves account when employee is deleted without tracked fixup");
      }
      using var cancelled = new CancellationTokenSource();
      cancelled.Cancel();
      await PostgresTests.ThrowsAsync<OperationCanceledException>(() => service.GetDetailsAsync(otherEmployee.Id, cancelled.Token),
        check, "employee details propagates cancellation after Identity decoupling");
    }
    finally
    {
      await using var db = await factory.CreateDbContextAsync();
      await db.Users.Where(u => u.Id.StartsWith(prefix)).ExecuteDeleteAsync();
      await db.Personals.Where(p => p.Id == employeeRow.Id || p.Id == otherEmployee.Id).ExecuteDeleteAsync();
      await db.Subdivisions.Where(s => s.Id == own.Id || s.Id == foreign.Id).ExecuteDeleteAsync();
    }
  }

  private static async Task ConstraintAsync(IDbContextFactory<ApplicationDbContext> factory, ApplicationUser user,
    string sqlState, Action<bool, string> check, string scenario)
  {
    await using var db = await factory.CreateDbContextAsync();
    db.Users.Add(user);
    try { await db.SaveChangesAsync(); check(false, scenario); }
    catch (DbUpdateException error) when (error.InnerException is PostgresException pg && pg.SqlState == sqlState)
    { check(true, scenario); }
  }

  private sealed class Access : IPermissionService
  {
    public UserAccessSnapshot Snapshot = new("boundary", false, [Permissions.Personals.Read, Permissions.Personals.ReadDetails]);
    public Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) => Task.FromResult(Snapshot);
    public Task<UserAccessSnapshot> GetForUserAsync(string id, CancellationToken ct = default) => Task.FromResult(Snapshot);
    public Task<bool> HasAsync(string code, CancellationToken ct = default) => Task.FromResult(Snapshot.Has(code));
    public Task RequireAsync(string code, CancellationToken ct = default)
    {
      ct.ThrowIfCancellationRequested();
      if (!Snapshot.Has(code)) throw new UnauthorizedAccessException();
      return Task.CompletedTask;
    }
  }

  private sealed class Scope(Guid own, Access access) : IDataAccessScopeService
  {
    public Task<DataAccessScope> GetAsync(CancellationToken ct = default) => Task.FromResult(new DataAccessScope("boundary", access.Snapshot.IsRoot, [own]));
  }
}
