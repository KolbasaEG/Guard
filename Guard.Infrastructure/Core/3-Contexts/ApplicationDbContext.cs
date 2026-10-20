using Guard.Core.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using Guard.Core.Logging;

namespace Guard.Core.Contexts;

/// <summary>
/// Основной DbContext приложения Guard.
/// 
/// Наследуется от IdentityDbContext с ключом string (стандарт ASP.NET Core Identity).
/// 
/// Особенности:
/// - Применяет все конфигурации из сборки через ApplyConfigurationsFromAssembly.
/// - Содержит DbSet для Subdivision (и будет добавляться по мере развития).
/// - Готов к добавлению глобальных query filters (например, soft delete по Status).
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
{
  // Стандартный конструктор EF Core без зависимости от Scoped-интерцептора
  public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, AuditPersistence? audit = null)
      : base(options)
  {
    AuditWriter = audit;
  }

  protected override void OnModelCreating(ModelBuilder builder)
  {
    base.OnModelCreating(builder);
    builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
  }

  public DbSet<IpAddress> IpAddresses { get; set; } = null!;
  public DbSet<Subdivision> Subdivisions { get; set; } = null!;
  public DbSet<Classifier> Classifiers { get; set; } = null!;
  public DbSet<Personal> Personals { get; set; } = null!;
  public DbSet<AuditEventRecord> AuditEvents => Set<AuditEventRecord>();
  public DbSet<AuditSettings> AuditSettings => Set<AuditSettings>();
  public DbSet<AuditDeliveryAttempt> AuditDeliveryAttempts => Set<AuditDeliveryAttempt>();
  public DbSet<ExportJob> ExportJobs { get; set; } = null!;
  public DbSet<ExportArtifact> ExportArtifacts { get; set; } = null!;
  public DbSet<ExportDeliveryAttempt> ExportDeliveryAttempts { get; set; } = null!;
  public DbSet<ExportRetentionSettings> ExportRetentionSettings { get; set; } = null!;
  public DbSet<SynchronizationSettings> SynchronizationSettings { get; set; } = null!;
  public DbSet<SynchronizationRun> SynchronizationRuns { get; set; } = null!;

  // Только прикладная операция синхронизации может обходить режим просмотра.
  internal bool IsSynchronization { get; set; }
  internal Action<ApplicationDbContext>? PrepareAudit { get; set; }
  internal string? AuditActorId { get; set; }
  internal AuditPersistence? AuditWriter { get; set; }
  internal bool IsAuditInitialization { get; set; }
  internal bool IsAuditMaintenance { get; set; }
  internal const long ManagementLock = 73460219;

  public override int SaveChanges(bool acceptAllChangesOnSuccess)
  {
    if (AuditWriter != null) return SaveChangesAsync(acceptAllChangesOnSuccess).GetAwaiter().GetResult();
    PrepareAudit?.Invoke(this);
    if (!IsSynchronization && HasDirectoryChanges())
      return SaveChangesAsync(acceptAllChangesOnSuccess).GetAwaiter().GetResult();
    PrepareVersions();
    return base.SaveChanges(acceptAllChangesOnSuccess);
  }

  public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
  {
    if (AuditWriter != null && Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
      return await AuditWriter.SaveAsync(this, () => SaveCoreAsync(acceptAllChangesOnSuccess, cancellationToken), cancellationToken);
    return await SaveCoreAsync(acceptAllChangesOnSuccess, cancellationToken);
  }

  private async Task<int> SaveCoreAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken)
  {
    PrepareAudit?.Invoke(this);
    if (IsSynchronization || !HasDirectoryChanges())
    {
      PrepareVersions();
      return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
    // Общая блокировка режима удерживается до завершения ручной записи.
    var ownTransaction = Database.IsRelational() && Database.CurrentTransaction == null;
    await using var transaction = ownTransaction ? await Database.BeginTransactionAsync(cancellationToken) : null;
    if (Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
      await Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({ManagementLock})", cancellationToken);
    var mode = await SynchronizationSettings.AsNoTracking().Where(s => s.Id == 1)
      .Select(s => s.Mode).SingleOrDefaultAsync(cancellationToken);
    if (mode == DataManagementMode.Api)
      throw new Guard.Core.Services.EntityRuleException("Данными управляет API. Доступен только просмотр.");
    PrepareVersions();
    var count = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    if (transaction != null) await transaction.CommitAsync(cancellationToken);
    return count;
  }

  private bool HasDirectoryChanges() => ChangeTracker.Entries().Any(e =>
    e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
    // Учётные записи и их привязки управляются локально, в том числе в режиме API.
    (e.Entity is Personal or Subdivision or Classifier || e.Metadata.GetTableName() == "PersonalIpAddresses"));

  private void PrepareVersions()
  {
    IpCatalogKeys.Prepare(this);
    foreach (var entry in ChangeTracker.Entries().Where(e =>
        e.State is EntityState.Added or EntityState.Modified &&
        e.Entity is Personal or Subdivision or Classifier or Guard.Core.Entities.SynchronizationSettings))
      entry.Property("Version").CurrentValue = Guid.NewGuid();
  }
}
