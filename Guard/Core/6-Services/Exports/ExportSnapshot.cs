using System.Data;
using System.Text.Json;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services.Exports;

public sealed record ExportSnapshotInfo(long Count, int Depth, Guid[] SubdivisionIds, bool Unassigned);
public sealed class ExportSnapshot(IDbContextFactory<ApplicationDbContext> contexts, ExportStorage storage)
{
  public async Task<ExportSnapshotInfo> CaptureAsync(ExportJob job, ExportRequest request, ExportAccessScope scope,
    Func<CancellationToken, Task> checkpoint, CancellationToken ct)
  {
    await using var db = await contexts.CreateDbContextAsync(ct);
    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
    IQueryable<Guid?> ids; IAsyncEnumerable<RawRow> rows;
    switch (request.Area) {
      case ExportArea.Personals:
        var personals = await ExportQueries.PersonalsAsync(db, new PersonalSearchRequest(Mode: request.Mode, OrderBy: request.OrderBy,
          Filter: request.PersonalFilter, OwnSubdivision: !request.AllSubdivisions, AllSubdivisions: request.AllSubdivisions, IncludeChildren: request.IncludeChildren),
          scope.User, new DataAccessScope(scope.User.UserId, scope.User.IsRoot, scope.AllowedIds), ct);
        ids = personals.Select(p => p.SubdivisionId);
        rows = personals.Select(p => new RawRow(p.SubdivisionId, new PersonalExportDto(p.LastName, p.FirstName, p.MiddleName,
          p.Subdivision == null ? null : p.Subdivision.Name, ExportSchema.StatusName(p.Status)))).AsAsyncEnumerable(); break;
      case ExportArea.Subdivisions:
        var subdivisions = ExportQueries.Subdivisions(db, request, scope); ids = subdivisions.Select(s => (Guid?)s.Id);
        rows = subdivisions.Select(s => new RawRow(s.Id, new SubdivisionExportDto(s.Name, s.Parent == null ? null : s.Parent.Name,
          s.OrganType == null ? null : s.OrganType.Value, ExportSchema.StatusName(s.Status), s.PositionFormationName, s.PostalCode,
          s.Address, s.Phone, s.StaffCount, s.LevelOrder, s.IsDepartment))).AsAsyncEnumerable(); break;
      case ExportArea.IpAddresses:
        var ips = ExportQueries.IpAddresses(db, request, scope); ids = ips.Select(p => p.SubdivisionId);
        rows = ips.Select(p => new RawRow(p.SubdivisionId, new IpAddressExportDto(p.Address, p.Description,
          p.Subdivision == null ? null : p.Subdivision.Name, ExportSchema.StatusName(p.Status)))).AsAsyncEnumerable(); break;
      default: throw new ArgumentException("Неизвестная область экспорта.");
    }
    var selected = await ids.Distinct().ToListAsync(ct);
    var nodes = new Dictionary<Guid, ExportSubdivision>();
    var pending = selected.Where(id => id != null).Select(id => id!.Value).ToHashSet();
    for (var level = 0; pending.Count > 0; level++) {
      if (level >= 64) throw new InvalidOperationException("Слишком глубокая иерархия.");
      var parents = new HashSet<Guid>();
      foreach (var batch in pending.Chunk(1000)) {
        var part = await db.Subdivisions.AsNoTracking().Where(s => batch.Contains(s.Id))
          .Select(s => new ExportSubdivision(s.Id, s.ParentId, s.SubdivisionId, s.Name, s.Path)).ToListAsync(ct);
        foreach (var node in part) { nodes.TryAdd(node.Id, node); if (node.ParentId is Guid parent && !nodes.ContainsKey(parent)) parents.Add(parent); }
        if (part.Count != batch.Length) throw new InvalidOperationException("Подразделение выгрузки не найдено.");
      }
      pending = parents.Where(id => !nodes.ContainsKey(id)).ToHashSet();
    }
    var hierarchy = new ExportHierarchy(nodes.Values); var depth = 0;
    foreach (var id in selected) depth = Math.Max(depth, hierarchy.Get(id).Length);
    await using var output = new FileStream(Path.Combine(storage.WorkDirectory(job.Id), "rows.protected"), FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true);
    long index = 0;
    await foreach (var row in rows.WithCancellation(ct)) {
      if (index % 500 == 0) { await checkpoint(ct); storage.CheckSpace(); }
      var record = new ExportRow(row.SubdivisionId, hierarchy.Get(row.SubdivisionId), ExportSchema.Cells(request.Area, row.Dto));
      await storage.WriteFrameAsync(output, "rows:" + job.Id, index++, JsonSerializer.SerializeToUtf8Bytes(record), ct);
    }
    await storage.WriteFrameAsync(output, "rows:" + job.Id, index, ReadOnlyMemory<byte>.Empty, ct);
    await output.FlushAsync(ct); await transaction.CommitAsync(ct);
    return new(index, depth, selected.Where(id => id != null).Select(id => id!.Value).ToArray(), selected.Contains(null));
  }
  private sealed record RawRow(Guid? SubdivisionId, object Dto);
}
