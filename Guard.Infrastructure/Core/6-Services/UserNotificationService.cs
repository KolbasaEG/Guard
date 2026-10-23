using System.Data;
using System.Linq.Expressions;
using System.Text.Json;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services.DTOs;
using Guard.Core.Services.Exports;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

public sealed class UserNotificationService(IDbContextFactory<ApplicationDbContext> factory,
  IPermissionService permissions, INotificationSignals signals, ExportAccess exportAccess) : IUserNotificationService
{
  private static readonly Expression<Func<UserNotification, UserNotificationDto>> Projection = n => new(
    n.Id, n.Kind, n.Severity, n.Title, n.Message, n.Target, n.TargetId, n.IncidentActive,
    n.IncidentId != null, n.CreatedAtUtc, n.UpdatedAtUtc, n.Revision, n.ReadRevision >= n.Revision);

  private static IQueryable<UserNotification> Own(ApplicationDbContext db, UserAccessSnapshot actor) =>
    db.UserNotifications.Where(n => n.UserId == actor.UserId && (!n.RootOnly || actor.IsRoot));

  private async Task RecheckAsync(UserAccessSnapshot actor, CancellationToken ct)
  {
    var current = await permissions.GetCurrentAsync(ct);
    if (current.UserId != actor.UserId || current.IsRoot != actor.IsRoot)
      throw new UnauthorizedAccessException("Права изменились. Обновите уведомления.");
  }

  public async Task<NotificationSummary> GetSummaryAsync(CancellationToken ct = default)
  {
    var actor = await permissions.GetCurrentAsync(ct);
    await using var db = await factory.CreateDbContextAsync(ct);
    await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
    var query = Own(db, actor).AsNoTracking();
    var total = await query.CountAsync(ct);
    var count = await query.CountAsync(n => n.ReadRevision < n.Revision, ct);
    var watermark = await query.MaxAsync(n => (long?)n.Revision, ct) ?? 0;
    var latest = await query.OrderByDescending(n => n.UpdatedAtUtc).ThenByDescending(n => n.Id).Take(10).Select(Projection).ToListAsync(ct);
    await RecheckAsync(actor, ct); await tx.CommitAsync(ct);
    return new(count, watermark, latest.AsReadOnly(), total);
  }

  public async Task<NotificationPage> SearchAsync(NotificationSearch query, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(query);
    if (query.Skip < 0 || query.Take is < 1 or > 100 || query.AfterRevision < 0 || query.ThroughRevision < query.AfterRevision)
      throw new ArgumentException("Некорректные параметры списка уведомлений.");
    var actor = await permissions.GetCurrentAsync(ct);
    await using var db = await factory.CreateDbContextAsync(ct);
    var rows = Own(db, actor).AsNoTracking().Where(n => n.Revision > query.AfterRevision && n.Revision <= query.ThroughRevision);
    if (query.UnreadOnly) rows = rows.Where(n => n.ReadRevision < n.Revision);
    var count = await rows.CountAsync(ct);
    var items = await rows.OrderByDescending(n => n.UpdatedAtUtc).ThenByDescending(n => n.Id)
      .Skip(query.Skip).Take(query.Take).Select(Projection).ToListAsync(ct);
    await RecheckAsync(actor, ct);
    return new(items.AsReadOnly(), count);
  }

  public async Task<UserNotificationDto?> GetAsync(Guid id, CancellationToken ct = default)
  {
    var actor = await permissions.GetCurrentAsync(ct);
    await using var db = await factory.CreateDbContextAsync(ct);
    var result = await Own(db, actor).AsNoTracking().Where(n => n.Id == id).Select(Projection).SingleOrDefaultAsync(ct);
    await RecheckAsync(actor, ct); return result;
  }

  public async Task MarkReadAsync(Guid id, long revision, CancellationToken ct = default)
  {
    if (revision <= 0) throw new ArgumentException("Некорректная версия уведомления.");
    var actor = await permissions.GetCurrentAsync(ct);
    await using var db = await factory.CreateDbContextAsync(ct);
    await using var tx = await db.Database.BeginTransactionAsync(ct);
    await Own(db, actor).Where(n => n.Id == id && n.Revision == revision && n.ReadRevision < revision)
      .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadRevision, revision).SetProperty(n => n.ReadAtUtc, DateTime.UtcNow), ct);
    await RecheckAsync(actor, ct); await tx.CommitAsync(ct);
    signals.Invalidate(actor.UserId);
  }

  public async Task<UserNotificationDto> GetAccessibleTargetAsync(Guid id, CancellationToken ct = default)
  {
    var actor = await permissions.GetCurrentAsync(ct);
    await using var db = await factory.CreateDbContextAsync(ct);
    var item = await Own(db, actor).AsNoTracking().Where(n => n.Id == id).Select(Projection).SingleOrDefaultAsync(ct)
      ?? throw new UnauthorizedAccessException("Уведомление недоступно.");
    if (item.Target == NotificationTarget.Export && item.TargetId is Guid exportId) {
      var job = await db.ExportJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == exportId && j.AuthorId == actor.UserId, ct)
        ?? throw new UnauthorizedAccessException("Выгрузка недоступна.");
      var request = JsonSerializer.Deserialize<ExportRequest>(job.RequestJson) ?? throw new InvalidDataException("Параметры выгрузки недоступны.");
      var artifact = await db.ExportArtifacts.AsNoTracking().SingleOrDefaultAsync(a => a.JobId == exportId, ct);
      var scope = await exportAccess.ForUserAsync(actor.UserId, request, ct);
      if (artifact != null) ExportAccess.CheckDelivery(scope, job,
        JsonSerializer.Deserialize<Guid[]>(artifact.SubdivisionIdsJson) ?? [], artifact.IncludesUnassigned);
      await permissions.RequireAsync(ExportSchema.Permission(request.Area), ct);
      if (request.Mode == DataViewMode.Archived && request.Area != ExportArea.IpAddresses)
        await permissions.RequireAsync(request.Area == ExportArea.Personals ? Guard.Core.Identity.Permissions.Personals.ReadArchive : Guard.Core.Identity.Permissions.Subdivisions.ReadArchive, ct);
    } else {
      if (!actor.IsRoot || item.Target is not (NotificationTarget.Synchronization or NotificationTarget.TechnicalLogs))
        throw new UnauthorizedAccessException("Событие недоступно.");
      if (item.Target == NotificationTarget.TechnicalLogs) await permissions.RequireAsync(Guard.Core.Identity.Permissions.Logs.Read, ct);
    }
    await RecheckAsync(actor, ct); return item;
  }

  public async Task MarkAllReadAsync(long watermark, CancellationToken ct = default)
  {
    if (watermark < 0) throw new ArgumentException("Некорректная версия списка.");
    var actor = await permissions.GetCurrentAsync(ct);
    await using var db = await factory.CreateDbContextAsync(ct);
    await using var tx = await db.Database.BeginTransactionAsync(ct);
    await Own(db, actor).Where(n => n.Revision <= watermark && n.ReadRevision < n.Revision)
      .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadRevision, n => n.Revision).SetProperty(n => n.ReadAtUtc, DateTime.UtcNow), ct);
    await RecheckAsync(actor, ct); await tx.CommitAsync(ct);
    signals.Invalidate(actor.UserId);
  }
}
