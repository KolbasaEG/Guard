using Guard.Core.Contexts;
using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

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
      ALTER TABLE "Personals" DROP COLUMN "Version";
      ALTER TABLE "Subdivisions" DROP COLUMN "Version";
      ALTER TABLE "Classifiers" DROP COLUMN "Version";
      ALTER TABLE "OrganTypes" DROP COLUMN "Version";
      ALTER TABLE "OrganTypes" ALTER COLUMN "Id" DROP IDENTITY IF EXISTS;
      ALTER TABLE "AspNetUsers"
        DROP COLUMN "AccountBlockReason", DROP COLUMN "CreatedAtUtc", DROP COLUMN "LastActivityAtUtc",
        DROP COLUMN "MustChangePassword", DROP COLUMN "PasswordChangedAtUtc", DROP COLUMN "UnblockedAtUtc";
      CREATE TABLE "__EFMigrationsHistory" ("MigrationId" varchar(150) PRIMARY KEY, "ProductVersion" varchar(32) NOT NULL);
      """);
    var migrations = db.Database.GetMigrations().ToArray();
    check(migrations.Any(m => m.EndsWith("_AddAccountPolicy")), "account policy migration exists");
    foreach (var migration in migrations.TakeWhile(m => !m.EndsWith("_AddAccountPolicy")))
      await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({migration}, {"10.0.12"})");
    await db.GetService<IMigrator>().MigrateAsync("20261008160000_AddEntityVersions");
    var legacy = await db.Users.SingleAsync(u => u.Id == "migration-legacy");
    check(legacy.CreatedAtUtc > DateTimeOffset.UtcNow.AddMinutes(-5) && legacy.PasswordChangedAtUtc == null && !legacy.MustChangePassword,
      "migration initializes legacy account without inventing password history");
    check(!await db.Set<AccountPolicy>().AnyAsync(), "migration leaves periodic policies disabled by default");
    // An existing policy must retain its settings when the new login fields are added.
    await db.Database.ExecuteSqlRawAsync("""
      INSERT INTO "AccountPolicies" ("Id", "Version", "PasswordExpirationEnabled", "PasswordDays", "InactivityEnabled", "InactivityDays", "MinimumLength", "UniqueCharacters", "RequireDigit", "RequireLowercase", "RequireUppercase", "RequireSymbol")
      VALUES (1, '00000000-0000-0000-0000-000000000001', false, 45, false, 60, 12, 4, true, true, true, true);
      """);
    await db.Database.MigrateAsync();
    var migratedPolicy = await db.Set<AccountPolicy>().SingleAsync();
    check(migratedPolicy.MaxFailedLoginAttempts == 5 && migratedPolicy.LoginLockoutMinutes == 15 && migratedPolicy.PasswordDays == 45 && migratedPolicy.MinimumLength == 12,
      "login policy migration initializes new defaults without changing existing password settings");
    db.Remove(migratedPolicy); await db.SaveChangesAsync();
    db.Users.Remove(legacy); await db.SaveChangesAsync();
    check(!(await db.Database.GetPendingMigrationsAsync()).Any(), "migration applied on isolated database");
  }
}
