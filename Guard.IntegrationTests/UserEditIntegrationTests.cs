using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

internal static class UserEditIntegrationTests
{
  // Only called inside the PostgreSQL harness's temporary database.
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory,
    IPermissionService root, IPermissionService ordinary, Action<bool, string> check)
  {
    var services = new ServiceCollection().AddLogging();
    services.AddScoped(_ => factory.CreateDbContext());
    services.AddIdentityCore<ApplicationUser>().AddRoles<ApplicationRole>().AddEntityFrameworkStores<ApplicationDbContext>();
    await using var provider = services.BuildServiceProvider();
    var notifier = new AccessChangeNotifier(NullLogger<AccessChangeNotifier>.Instance);
    SecurityService Service(IPermissionService permissions) => new(null!, permissions,
      new DataAccessScopeService(permissions, new DataAccessScopeReader(factory)), provider.GetRequiredService<IServiceScopeFactory>(), new AuditStub(), notifier, null!, NullLogger<SecurityService>.Instance);
    var service = Service(root);
    var person = new Personal { FirstName = "Edit", LastName = "Test", CreatedBy = "root" };
    var takenPerson = new Personal { FirstName = "Taken", LastName = "Test", CreatedBy = "root" };
    var target = new ApplicationUser { UserName = "edit-test", NormalizedUserName = "EDIT-TEST", Email = "edit@example.test",
      SecurityStamp = "original", ConcurrencyStamp = "original", EmailConfirmed = true };
    var taken = new ApplicationUser { UserName = "edit-taken", NormalizedUserName = "EDIT-TAKEN", PersonalId = takenPerson.Id };
    await using (var db = await factory.CreateDbContextAsync()) {
      db.AddRange(person, takenPerson, target, taken);
      db.UserRoles.Add(new() { UserId = target.Id, RoleId = "admin-role" });
      await db.SaveChangesAsync();
      var settings = await db.SynchronizationSettings.SingleAsync(s => s.Id == 1);
      settings.Mode = DataManagementMode.Api;
      await db.SaveChangesAsync();
    }
    var data = await service.GetUserEditAsync(target.Id);
    check(data.IsRoot && data.Personals.Any(p => p.Id == person.Id) && !data.Personals.Any(p => p.Id == takenPerson.Id),
      "Root without subdivision can edit and choose only free employees");
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => Service(ordinary).GetUserEditAsync(target.Id), check,
      "ordinary manager cannot open a user outside their subdivision");
    var input = data.User;
    input.PersonalId = takenPerson.Id;
    await PostgresTests.ThrowsAsync<InvalidOperationException>(() => service.SaveDetailsAsync(input), check,
      "server rejects tampered employee already assigned to another user");
    input.PersonalId = person.Id; input.UserName = "edited-test"; input.Email = "changed@example.test";
    await service.SaveDetailsAsync(input);
    await using (var db = await factory.CreateDbContextAsync()) {
      var saved = await db.Users.SingleAsync(u => u.Id == target.Id);
      check(saved.UserName == input.UserName && saved.Email == input.Email && saved.PersonalId == person.Id &&
        !saved.EmailConfirmed && saved.SecurityStamp != "original" &&
        await db.UserRoles.AnyAsync(r => r.UserId == target.Id && r.RoleId == "admin-role"),
        "edit preserves roles, resets email confirmation and invalidates sessions");
    }
    input.UserName = "stale-overwrite";
    await PostgresTests.ThrowsAsync<InvalidOperationException>(() => service.SaveDetailsAsync(input), check,
      "stale edit cannot overwrite newer user details");
    var fresh = (await service.GetUserEditAsync(target.Id)).User;
    await using (var db = await factory.CreateDbContextAsync()) {
      var saved = await db.Users.SingleAsync(u => u.Id == target.Id);
      saved.AccountBlockReason = "Administrator"; await db.SaveChangesAsync();
    }
    await PostgresTests.ThrowsAsync<InvalidOperationException>(() => service.SaveDetailsAsync(fresh), check,
      "blocking account after form load prevents saving");
    await using (var db = await factory.CreateDbContextAsync()) {
      check((await db.Users.SingleAsync(u => u.Id == target.Id)).UserName == "edited-test", "rejected edits leave persisted fields unchanged");
      db.Users.RemoveRange(await db.Users.Where(u => u.Id == target.Id || u.Id == taken.Id).ToListAsync());
      await db.SaveChangesAsync();
      var settings = await db.SynchronizationSettings.SingleAsync(s => s.Id == 1);
      settings.Mode = DataManagementMode.Local;
      await db.SaveChangesAsync();
      db.Personals.RemoveRange(await db.Personals.Where(p => p.Id == person.Id || p.Id == takenPerson.Id).ToListAsync());
      await db.SaveChangesAsync();
    }
  }
}
