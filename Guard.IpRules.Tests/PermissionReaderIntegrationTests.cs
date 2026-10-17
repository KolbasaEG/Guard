using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Services;
using Microsoft.EntityFrameworkCore;

internal static class PermissionReaderIntegrationTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory, Action<bool, string> check)
  {
    var id = "permission-reader-" + Guid.NewGuid().ToString("N");
    var reader = new PermissionReader(factory);
    check(await reader.FindAccessAsync(id) == null && await reader.GetSecurityStampAsync(id) == null,
      "permission reader denies missing stored account");
    await using var db = await factory.CreateDbContextAsync();
    db.Users.Add(new ApplicationUser { Id = id, UserName = id, SecurityStamp = "before" });
    db.Roles.Add(new ApplicationRole { Id = id, Name = id, NormalizedName = id.ToUpperInvariant() });
    db.UserRoles.Add(new() { UserId = id, RoleId = id });
    db.RoleClaims.AddRange(new() { RoleId = id, ClaimType = "Permission", ClaimValue = Permissions.Personals.Read },
      new() { RoleId = id, ClaimType = "unrelated", ClaimValue = Permissions.Roles.Manage });
    db.UserClaims.Add(new() { UserId = id, ClaimType = "Permission", ClaimValue = Permissions.Roles.Manage });
    await db.SaveChangesAsync();
    try {
      var data = (await reader.FindAccessAsync(id))!;
      check(data.RoleNames.SequenceEqual([id.ToUpperInvariant()]) && data.PermissionCodes.SequenceEqual([Permissions.Personals.Read]),
        "reader materializes only assigned role permissions, excluding individual and unrelated claims");
      check(await reader.GetSecurityStampAsync(id) == "before", "reader loads stored stamp");
      await db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(s => s.SetProperty(u => u.SecurityStamp, "after"));
      check(await reader.GetSecurityStampAsync(id) == "after", "same reader observes revoked stamp without tracking cache");
      foreach (var (enabled, end, reason, available) in new (bool, DateTimeOffset?, string?, bool)[] {
        (true, null, null, true), (true, DateTimeOffset.UtcNow.AddHours(-1), null, true),
        (true, DateTimeOffset.UtcNow.AddHours(1), null, false),
        (false, DateTimeOffset.UtcNow.AddHours(1), null, true), (false, null, "Administrator", false) }) {
        await db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(s => s
          .SetProperty(u => u.LockoutEnabled, enabled).SetProperty(u => u.LockoutEnd, end)
          .SetProperty(u => u.AccountBlockReason, reason));
        check((await reader.FindAccessAsync(id) != null) == available, "same reader rereads block and lockout conditions");
      }
      using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
      await PostgresTests.ThrowsAsync<OperationCanceledException>(() => reader.GetSecurityStampAsync(id, cancelled.Token), check, "stamp read honors cancellation");
      await PostgresTests.ThrowsAsync<OperationCanceledException>(() => reader.FindAccessAsync(id, cancelled.Token), check, "role read honors cancellation");
    }
    finally {
      await db.Users.Where(u => u.Id == id).ExecuteDeleteAsync();
      await db.Roles.Where(r => r.Id == id).ExecuteDeleteAsync();
    }
  }
}
