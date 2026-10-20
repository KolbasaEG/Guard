using System.Text.Json;
using Guard.Core.Contexts;
using Guard.Core.Enums;
using Guard.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Guard.Core.Logging;

/// <summary>Единая атомарность SaveChanges, включая прямые фабричные и Identity-контексты.</summary>
public sealed class AuditPersistence(AuditJournal journal)
{
  internal Task RequireWorkingTransactionAsync(ApplicationDbContext db, CancellationToken ct) => journal.CheckTransactionAsync(db, ct);
  internal async Task<int> SaveAsync(ApplicationDbContext db, Func<Task<int>> save, CancellationToken ct)
  {
    db.ChangeTracker.DetectChanges();
    if (!db.ChangeTracker.Entries().Any(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
      e.Metadata.GetTableName() is not ("AuditEvents" or "AuditSettings" or "AuditDeliveryAttempts"))) return await save();
    var own = db.Database.CurrentTransaction == null;
    await using var tx = own ? await db.Database.BeginTransactionAsync(ct) : null;
    var entities = db.IsSynchronization ? null : new EntityChangeAudit(db);
    var security = CaptureSecurity(db);
    try {
      var result = await save();
      if (!db.IsAuditInitialization && !db.IsAuditMaintenance) await journal.CheckTransactionAsync(db, ct);
      foreach (var entry in (entities?.Complete(db.AuditActorId) ?? []).Concat(security))
        await journal.WriteInTransactionAsync(db, entry, db.IsAuditInitialization, ct);
      if (tx != null) await AuditTransaction.CommitAsync(tx, ct);
      return result;
    }
    catch (OperationCommitUnknownException) { throw; }
    catch {
      if (tx != null) await tx.RollbackAsync(CancellationToken.None);
      throw;
    }
  }

  private static List<AuditEventData> CaptureSecurity(ApplicationDbContext db)
  {
    var result = new List<AuditEventData>();
    var allowed = new HashSet<string> { "PersonalId", "LockoutEnabled", "LockoutEnd", "AccessFailedCount", "AccountBlockReason",
      "MustChangePassword", "RoleId", "UserId", "ClaimType", "ClaimValue", "Name", "NormalizedName", "PasswordDays",
      "InactivityDays", "MaxFailedLoginAttempts", "LoginLockoutMinutes", "Years", "PasswordExpirationEnabled",
      "PasswordEnabledAtUtc", "InactivityEnabled", "InactivityEnabledAtUtc", "MinimumLength", "UniqueCharacters",
      "RequireDigit", "RequireLowercase", "RequireUppercase", "RequireSymbol" };
    var ignored = new HashSet<string> { "ConcurrencyStamp", "LastActivityAtUtc" };
    foreach (var e in db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)) {
      var table = e.Metadata.GetTableName();
      if (table is not ("AspNetUsers" or "AspNetRoles" or "AspNetUserRoles" or "AspNetRoleClaims" or "AspNetUserClaims" or "AccountPolicies")) continue;
      var fields = e.Properties.Where(p => !ignored.Contains(p.Metadata.Name) && (e.State != EntityState.Modified || p.IsModified)).ToArray();
      if (fields.Length == 0) continue;
      var id = table is "AspNetUserRoles" or "AspNetUserClaims" ? e.Property("UserId").CurrentValue?.ToString()
        : table == "AspNetRoleClaims" ? e.Property("RoleId").CurrentValue?.ToString()
        : string.Join("/", e.Properties.Where(p => p.Metadata.IsPrimaryKey()).Select(p => p.CurrentValue));
      var type = table switch {
        "AspNetUsers" when e.State == EntityState.Added => AuditEventType.UserCreated,
        "AspNetUserRoles" => e.State == EntityState.Deleted ? AuditEventType.RoleRemoved : AuditEventType.RoleAssigned,
        "AspNetRoles" when e.State == EntityState.Added => AuditEventType.RoleCreated,
        "AspNetRoles" when e.State == EntityState.Deleted => AuditEventType.RoleDeleted,
        "AspNetRoles" or "AspNetRoleClaims" => AuditEventType.RolePermissionsChanged,
        "AccountPolicies" => AuditEventType.AccountPolicyChanged,
        _ => AuditEventType.UserDetailsChanged
      };
      var values = fields.Where(p => allowed.Contains(p.Metadata.Name) &&
        (p.Metadata.Name != "ClaimValue" || e.Property("ClaimType").CurrentValue?.ToString() == "Permission"))
        .ToDictionary(p => p.Metadata.Name, p => (object)new {
          Before = e.State == EntityState.Added ? null : p.OriginalValue,
          After = e.State == EntityState.Deleted ? null : p.CurrentValue
        });
      result.Add(AuditEventData.Create(type, db.AuditActorId, details: JsonSerializer.Serialize(new {
        Operation = e.State.ToString(), ChangedFields = fields.Select(p => p.Metadata.Name), Fields = values
      })) with { ObjectType = table, ObjectId = id });
    }
    return result;
  }
}

internal static class AuditTransaction
{
  internal static async Task CommitAsync(IDbContextTransaction transaction, CancellationToken ct)
  {
    try { await transaction.CommitAsync(ct); }
    catch (Exception error) when (error is Npgsql.NpgsqlException or IOException or TimeoutException or OperationCanceledException) {
      throw new OperationCommitUnknownException(AuditOperationContext.Current?.CorrelationId, error);
    }
  }
  internal static void Attach(ApplicationDbContext db, IAuditService? audit)
  {
    if (db.AuditWriter == null && audit is AuditService { Journal: not null } service)
      db.AuditWriter = new AuditPersistence(service.Journal);
  }

  internal static async Task RecordAsync(ApplicationDbContext db, IAuditService audit, AuditEventData entry, CancellationToken ct)
  {
    if (audit is AuditService { Journal: not null } service) await service.Journal.WriteInTransactionAsync(db, entry, AuditJournal.IsServiceEvent(entry), ct);
    else await audit.RecordAsync(entry, ct);
  }
}
