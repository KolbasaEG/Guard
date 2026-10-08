using Guard.Core.Contexts;
using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;

internal static class AccountPolicyMigrationTests
{
  // Только временная БД тестового набора. Воссоздаём схему до новой миграции.
  public static async Task RunAsync(ApplicationDbContext db, Action<bool, string> check)
  {
    db.Users.Add(new ApplicationUser { Id = "migration-legacy", UserName = "legacy", SecurityStamp = "test" });
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();
    await db.Database.ExecuteSqlRawAsync("""
      DROP TABLE "AccountPolicies";
      ALTER TABLE "AspNetUsers"
        DROP COLUMN "AccountBlockReason", DROP COLUMN "CreatedAtUtc", DROP COLUMN "LastActivityAtUtc",
        DROP COLUMN "MustChangePassword", DROP COLUMN "PasswordChangedAtUtc", DROP COLUMN "UnblockedAtUtc";
      CREATE TABLE "__EFMigrationsHistory" ("MigrationId" varchar(150) PRIMARY KEY, "ProductVersion" varchar(32) NOT NULL);
      """);
    var migrations = db.Database.GetMigrations().ToArray();
    check(migrations[^1].EndsWith("_AddAccountPolicy"), "account policy migration is latest");
    foreach (var migration in migrations[..^1])
      await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({migration}, {"10.0.12"})");
    await db.Database.MigrateAsync();
    var legacy = await db.Users.SingleAsync(u => u.Id == "migration-legacy");
    check(legacy.CreatedAtUtc > DateTimeOffset.UtcNow.AddMinutes(-5) && legacy.PasswordChangedAtUtc == null && !legacy.MustChangePassword,
      "migration initializes legacy account without inventing password history");
    check(!await db.Set<AccountPolicy>().AnyAsync(), "migration leaves periodic policies disabled by default");
    db.Users.Remove(legacy); await db.SaveChangesAsync();
    check(!(await db.Database.GetPendingMigrationsAsync()).Any(), "migration applied on isolated database");
  }
}
