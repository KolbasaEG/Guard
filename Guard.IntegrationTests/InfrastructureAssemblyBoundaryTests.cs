using System.Data;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Services;
using Guard.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

internal static class InfrastructureAssemblyBoundaryTests
{
  public static void Run(Action<bool, string> check)
  {
    var infrastructure = typeof(InfrastructureServiceRegistration).Assembly;
    var host = typeof(Guard.GuardServiceRegistration).Assembly;
    check(infrastructure.GetName().Name == "Guard.Infrastructure" && infrastructure != host &&
      infrastructure != typeof(ICurrentSession).Assembly && infrastructure != typeof(Personal).Assembly,
      "Infrastructure is a fourth independent production assembly");
    foreach (var reference in infrastructure.GetReferencedAssemblies())
      check(reference.Name != host.GetName().Name && reference.Name != "Guard.IntegrationTests",
        $"Infrastructure has no reverse assembly reference: {reference.Name}");
    check(!infrastructure.GetTypes().Any(t => t.Namespace?.StartsWith("Guard.Components", StringComparison.Ordinal) == true ||
      t.Namespace?.StartsWith("Guard.Web", StringComparison.Ordinal) == true),
      "Infrastructure contains no Web components or circuit adapters");
    foreach (var type in infrastructure.GetExportedTypes())
      check(host.GetType(type.FullName!) == null && typeof(ICurrentSession).Assembly.GetType(type.FullName!) == null &&
        typeof(Personal).Assembly.GetType(type.FullName!) == null,
        $"Infrastructure owns {type.Name} without duplicate definition");

    // Baseline migration IDs from 7e64fc3: moving assemblies must not lose or rename history.
    string[] expectedMigrations = [
      "20260610123225_InitialCreate",
      "20260717140552_AddIpAddressCatalogWithManyToMany",
      "20260720090311_123123123",
      "20260723120841_AddIp",
      "20260805121645_AddSubdivision",
      "20260915073735_AddClassifiers",
      "20260915075544_AddClassifiers1",
      "20260917085422_ChangeSub",
      "20260921110232_AddPersonals",
      "20260921110549_AddPersonals1",
      "20260921123101_AddPersonals2",
      "20260922071849_Mig11",
      "20260922082844_Mig12",
      "20260922090427_Mig13",
      "20260922092706_Mig14",
      "20260922093211_Mig15",
      "20260922110604_Addop4",
      "20260922123419_Addop43",
      "20260922130426_Addop433",
      "20260923074544_Apt67",
      "20260923075328_Apt617",
      "20260923124507_Awd22",
      "20260929065524_Mig554",
      "20260929070747_Mig5534",
      "20260929113434_ad-ng",
      "20260929121404_ad-ng1",
      "20260929121910_ad-ng13",
      "20261001174220_fgr12",
      "20261008100218_AddAccountPolicy",
      "20261008160000_AddEntityVersions",
      "20261009090000_AddLoginFailurePolicy",
      "20261009103606_AddApiSynchronization",
      "20261009114922_AddApiSourceSettings",
      "20261009120337_AddApiBasicCredentials",
      "20261009133225_AddSynchronizationUpdatedSince",
      "20261010130000_AddApiHeadSubdivision",
      "20261014073616_AddApiExitEventsAndQueue",
      "20261014080024_AddApiRecoveryWindow",
      "20261014132937_AddDataExport",
      "20261015174116_AddApiSnapshotConfirmation",
      "20261017120000_AddIpCanonicalKeys",
      "20261017160000_AddPersonalActiveNameIndex",
    ];
    using var db = new ApplicationDbContextDesignFactory().CreateDbContext([]);
    var migrations = db.GetService<IMigrationsAssembly>();
    check(migrations.Assembly == infrastructure && migrations.ModelSnapshot?.GetType().Assembly == infrastructure,
      "EF discovers migrations and snapshot alongside the context in Infrastructure");
    check(db.Database.GetMigrations().SequenceEqual(expectedMigrations),
      "all 40 baseline migrations and the IP key and employee index migrations keep chronological order");
    check(!db.Database.HasPendingModelChanges(), "extraction preserves EF configuration discovery and relational snapshot");
    var script = db.GetService<IMigrator>().GenerateScript();
    check(expectedMigrations.All(id => script.Contains(id, StringComparison.Ordinal)),
      "offline SQL generation discovers the complete historical migration chain");
    check(db.Database.GetDbConnection().State == ConnectionState.Closed,
      "design-time context and migration script checks do not open a database connection");

    foreach (var (url, user, expected) in new (string?, string?, bool)[] {
      (null, null, false), (" https://api/// ", " user ", false), ("https://api", null, false),
      (null, "user", false), ("", null, true), (null, "", true),
      ("https://other", "user", true), ("https://API", "user", true), (null, "User", true) }) {
      check(SynchronizationSourceRules.SourceChanged("https://api", "user", url, user) == expected &&
        SynchronizationSettingsRules.SourceChanged("https://api", "user", url, user) == expected,
        "UI/Application and infrastructure agree on source changes, optional fields and normalization");
    }
    check(typeof(SynchronizationSourceRules).Assembly == typeof(ICurrentSession).Assembly,
      "shared source comparison belongs to independent Application rules");
  }
}
