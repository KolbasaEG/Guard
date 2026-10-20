using System.Collections.Frozen;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Logging;
using Guard.Core.Services.Exports;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

public static class AuditSearchFilter
{
  private static readonly FrozenDictionary<string, Type> Types = new Dictionary<string, Type> {
    ["EventId"] = typeof(Guid), ["OccurredAtUtc"] = typeof(DateTime), ["EventType"] = typeof(AuditEventType),
    ["Actor"] = typeof(string), ["ActorKind"] = typeof(string), ["Outcome"] = typeof(string),
    ["ClientIp"] = typeof(string), ["CorrelationId"] = typeof(string), ["ObjectType"] = typeof(string),
    ["ObjectId"] = typeof(string), ["Attempts"] = typeof(int), ["SentAtUtc"] = typeof(DateTime?),
    ["NextAttemptAtUtc"] = typeof(DateTime), ["LastError"] = typeof(string)
  }.ToFrozenDictionary(StringComparer.Ordinal);
  public static readonly FrozenSet<string> Fields = Types.Keys.ToFrozenSet(StringComparer.Ordinal);
  public static void Validate(ExportFilter? filter) => SearchFilterValidation.Validate(filter, Types);
}

public sealed class AuditSearchService(IDbContextFactory<ApplicationDbContext> factory, IPermissionService permissions,
  AuditHealth health, IAuditAdministrationService? administration = null) : IAuditSearchService
{
  private static IQueryable<AuditEntryDto> Project(IQueryable<AuditEventRecord> query) => query.Select(e =>
    new AuditEntryDto(e.EventId, e.OccurredAtUtc, e.EventType, e.Actor, e.ActorKind, e.Outcome, e.ClientIp,
      e.CorrelationId, e.ObjectType, e.ObjectId, e.Attempts, e.SentAtUtc, e.NextAttemptAtUtc, e.LastError,
      e.DeliveryRequired, e.ReasonCode, e.AttemptedAccount));

  private async Task CheckRootAsync(CancellationToken ct) {
    if (administration != null) await administration.RequireRootAsync(ct);
    else await SearchAccess.RequireAsync(permissions, Permissions.Audit.Read, ct);
  }

  public async Task<AuditSearchResult> SearchAsync(AuditSearchRequest request, CancellationToken ct = default)
  {
    await CheckRootAsync(ct);
    ArgumentNullException.ThrowIfNull(request);
    if (request.Skip < 0 || request.Take is < 1 or > 100 || request.DeliveryState is not ("all" or "pending" or "sent" or "retry" or "local"))
      throw new ArgumentException("Недопустимые параметры журнала аудита.");
    AuditSearchFilter.Validate(request.Filter);
    await using var db = await factory.CreateDbContextAsync(ct);
    var query = db.AuditEvents.AsNoTracking();
    query = request.DeliveryState switch {
      "pending" => query.Where(e => e.DeliveryRequired && e.SentAtUtc == null), "sent" => query.Where(e => e.SentAtUtc != null),
      "retry" => query.Where(e => e.DeliveryRequired && e.SentAtUtc == null && e.LastError != null),
      "local" => query.Where(e => !e.DeliveryRequired), _ => query
    };
    query = RestrictedSearchQuery<AuditEventRecord>.Filter(query, request.Filter, AuditSearchFilter.Fields);
    var count = await query.CountAsync(ct);
    var sorted = RestrictedSearchQuery<AuditEventRecord>.Sort(query, request.OrderBy, AuditSearchFilter.Fields, "OccurredAtUtc desc", "EventId");
    var items = await Project(sorted.Skip(request.Skip).Take(request.Take)).ToListAsync(ct);
    await CheckRootAsync(ct);
    return new(items, count);
  }

  public async Task<AuditDetailsDto?> GetAsync(Guid eventId, CancellationToken ct = default)
  {
    await CheckRootAsync(ct);
    await using var db = await factory.CreateDbContextAsync(ct);
    var query = db.AuditEvents.AsNoTracking().Where(e => e.EventId == eventId);
    var value = await query.Select(e => new { Entry = new AuditEntryDto(e.EventId, e.OccurredAtUtc, e.EventType, e.Actor,
      e.ActorKind, e.Outcome, e.ClientIp, e.CorrelationId, e.ObjectType, e.ObjectId, e.Attempts, e.SentAtUtc,
      e.NextAttemptAtUtc, e.LastError, e.DeliveryRequired, e.ReasonCode, e.AttemptedAccount), e.Details, e.SchemaVersion }).SingleOrDefaultAsync(ct);
    var attempts = await db.AuditDeliveryAttempts.AsNoTracking().Where(a => a.EventId == eventId)
      .OrderByDescending(a => a.StartedAtUtc).ThenBy(a => a.Id).Take(100)
      .Select(a => new AuditDeliveryAttemptDto(a.Id, a.StartedAtUtc, a.CompletedAtUtc, a.Destination, a.Succeeded, a.Error)).ToListAsync(ct);
    await CheckRootAsync(ct);
    return value == null ? null : new(value.Entry, value.Details, value.SchemaVersion, attempts);
  }

  public async Task<AuditQueueHealthDto> GetHealthAsync(CancellationToken ct = default)
  {
    await CheckRootAsync(ct);
    await using var db = await factory.CreateDbContextAsync(ct);
    var pending = db.AuditEvents.Where(e => e.DeliveryRequired && e.SentAtUtc == null);
    var result = new AuditQueueHealthDto(await pending.CountAsync(ct), await pending.CountAsync(e => e.LastError != null, ct),
      await pending.Select(e => e.QueuedAtUtc).MinAsync(ct),
      await db.AuditEvents.Select(e => e.SentAtUtc).MaxAsync(ct), health.StorageFailures);
    await CheckRootAsync(ct);
    return result;
  }
}
