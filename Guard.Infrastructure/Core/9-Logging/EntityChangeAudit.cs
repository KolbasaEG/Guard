using Guard.Core.Contexts;
using Guard.Core.Enums;
using Guard.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

namespace Guard.Core.Logging;

internal sealed class EntityChangeAudit
{
  private sealed record Change(EntityEntry Entry, EntityState State, Dictionary<string, object?> Before, string[] ChangedFields);
  private readonly List<Change> changes = [];
  private static readonly HashSet<string> Tables = ["Personals", "Subdivisions", "IpAddresses", "Classifiers", "PersonalIpAddresses"];
  // Разрешённые поля: идентификаторы, связи, статусы и справочники. Пароли, токены и ФИО исключены.
  private static readonly HashSet<string> Fields = ["Status", "SubdivisionId", "ParentId", "Path", "Name", "Value", "Address", "PersonalId", "IpAddressId", "IsActive", "Type", "Code", "ClassifierName"];

  public EntityChangeAudit(ApplicationDbContext db)
  {
    db.ChangeTracker.DetectChanges();
    foreach (var entry in db.ChangeTracker.Entries().Where(e => Tables.Contains(e.Metadata.GetTableName() ?? "") &&
      e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
      changes.Add(new(entry, entry.State, entry.Properties.Where(p => Fields.Contains(p.Metadata.Name))
        .ToDictionary(p => p.Metadata.Name, p => entry.State == EntityState.Added ? null : p.OriginalValue),
        entry.Properties.Where(p => entry.State != EntityState.Modified || p.IsModified).Select(p => p.Metadata.Name).ToArray()));
  }

  public IReadOnlyList<AuditEventData> Complete(string? actor)
  {
    var result = new List<AuditEventData>();
    foreach (var change in changes) {
      var fields = new Dictionary<string, object?>();
      foreach (var (name, before) in change.Before) {
        var after = change.State == EntityState.Deleted ? null : change.Entry.Property(name).CurrentValue;
        if (!Equals(before, after)) fields[name] = new { Before = before, After = after };
      }
      var key = string.Join("/", change.Entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).Select(p => p.CurrentValue));
      result.Add(AuditEventData.Create(AuditEventType.EntityChanged, actor,
        details: JsonSerializer.Serialize(new { Operation = change.State.ToString(), change.ChangedFields, Fields = fields })) with {
          ActorKind = actor == null ? "system" : "user", ObjectType = change.Entry.Metadata.GetTableName(), ObjectId = key
        });
    }
    return result;
  }

  internal static EntityChangeAudit? TryCapture(ApplicationDbContext db, IAuditService? audit)
  {
    return audit == null || db.AuditWriter != null ? null : new(db);
  }

  internal static async Task PublishAsync(EntityChangeAudit? captured, string? actor, IAuditService? audit, CancellationToken ct = default)
  {
      if (captured == null || audit == null) return;
      foreach (var entry in captured.Complete(actor)) {
        await audit.RecordAsync(entry, ct);
      }
  }
  private static void Failed(Exception error) => new InternalLogWriter().Write($"EntityAuditFailed ErrorType={error.GetType().Name}");
}
