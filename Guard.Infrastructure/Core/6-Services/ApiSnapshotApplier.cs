using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Interceptors;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

public static class ApiSnapshotApplier
{
  public static async Task<IReadOnlyCollection<Guid>> ApplyAsync(ApplicationDbContext db, ApiSnapshot snapshot,
    SynchronizationRun run, CancellationToken ct)
  {
    var subdivisions = await db.Subdivisions.ToListAsync(ct);
    var exitMarkers = await db.Set<ApiPersonnelExit>().ToDictionaryAsync(e => e.PersonalExternalId, ct);
    var knownExits = exitMarkers.ToDictionary(e => e.Key, e => e.Value.EventAt);
    if (snapshot.UpdatedSince.HasValue)
      ApiSnapshotValidator.Validate(snapshot,
        await db.Classifiers.Select(c => new ValueTuple<int, int>(c.Type, c.Code)).ToListAsync(ct),
        subdivisions.Where(s => s.ExternalId.HasValue).ToDictionary(s => s.ExternalId!.Value, s => s.ExternalParentId), knownExitEvents: knownExits);
    else ApiSnapshotValidator.Validate(snapshot, knownExitEvents: knownExits);
    await AlignSequencesAsync(db, ct);
    var now = DateTime.UtcNow;
    var classifiers = await db.Classifiers.ToListAsync(ct);
    var classifierMap = classifiers.ToDictionary(c => (c.Type, c.Code));
    var remoteKeys = snapshot.Classifiers.Select(c => (c.Type, c.Code)).ToHashSet();
    foreach (var dto in snapshot.Classifiers)
    {
      if (!classifierMap.TryGetValue((dto.Type, dto.Code), out var entity))
      {
        entity = new Classifier { Type = dto.Type, Code = dto.Code };
        db.Classifiers.Add(entity); classifierMap.Add((dto.Type, dto.Code), entity); run.Added++;
      }
      else if (entity.ClassifierName != dto.ClassifierName || entity.Value != dto.Value ||
        entity.IsActive != dto.IsActiveBool || entity.SourceUpdatedAt != SourceTime(dto.UpdatedAt) || !entity.ImportedFromApi)
      {
        run.Updated++; entity.UpdatedAt = now;
      }
      entity.ClassifierName = dto.ClassifierName; entity.Value = dto.Value;
      entity.IsActive = dto.IsActiveBool; entity.ImportedFromApi = true;
      entity.SourceUpdatedAt = SourceTime(dto.UpdatedAt);
    }
    foreach (var c in classifiers.Where(c => !snapshot.UpdatedSince.HasValue && c.ImportedFromApi && !remoteKeys.Contains((c.Type, c.Code)) && c.IsActive))
    {
      c.IsActive = false; run.Deactivated++;
    }

    var localMap = subdivisions.Where(s => s.ExternalId.HasValue).ToDictionary(s => s.ExternalId!.Value);
    var remoteMap = snapshot.Subdivisions.ToDictionary(s => s.Id);
    HashSet<Guid> previousPool;
    HashSet<long> sourcePool;
    if (run.PoolId.HasValue)
    {
      var root = subdivisions.SingleOrDefault(s => s.Id == run.PoolId.Value)
        ?? throw new EntityRuleException("Подразделение пула не найдено.");
      if (!root.ExternalId.HasValue || string.IsNullOrWhiteSpace(root.Path))
        throw new EntityRuleException("Пул ещё не сопоставлен с API. Требуется полная синхронизация Root.");
      previousPool = subdivisions.Where(s => s.Path.StartsWith(root.Path, StringComparison.Ordinal)).Select(s => s.Id).ToHashSet();
      sourcePool = Descendants(remoteMap, root.ExternalId.Value);
    }
    else
    {
      var head = snapshot.HeadSubdivisionId.HasValue ? subdivisions.SingleOrDefault(s => s.ExternalId == snapshot.HeadSubdivisionId) : null;
      previousPool = snapshot.HeadSubdivisionId.HasValue
        ? subdivisions.Where(s => head != null && !string.IsNullOrWhiteSpace(head.Path) && s.Path.StartsWith(head.Path, StringComparison.Ordinal)).Select(s => s.Id).ToHashSet()
        : subdivisions.Select(s => s.Id).ToHashSet();
      sourcePool = remoteMap.Keys.Union(subdivisions.Where(s => previousPool.Contains(s.Id) && s.ExternalId.HasValue).Select(s => s.ExternalId!.Value)).ToHashSet();
    }
    var selected = sourcePool.Union(subdivisions.Where(s => previousPool.Contains(s.Id) && s.ExternalId.HasValue)
      .Select(s => s.ExternalId!.Value)).ToHashSet();
    var personnel = await db.Personals.ToListAsync(ct);
    var personalMap = personnel.Where(p => p.ExternalId.HasValue).ToDictionary(p => p.ExternalId!.Value);
    var remotePersonnel = snapshot.Personnel.ToDictionary(p => p.Id);
    var selectedPeople = snapshot.Personnel.Where(p => sourcePool.Contains(p.SubdivisionId) ||
      personalMap.TryGetValue(p.Id, out var local) && local.SubdivisionId.HasValue && previousPool.Contains(local.SubdivisionId.Value)).ToList();
    selected.UnionWith(selectedPeople.Select(p => p.SubdivisionId));
    // Включаем новые цепочки родителей для переводов за границы прежнего пула.
    foreach (var id in selected.ToArray())
    {
      var next = id;
      while (next != snapshot.HeadSubdivisionId && remoteMap.TryGetValue(next, out var current) && current.ParentId is > 0)
      {
        selected.Add(current.ParentId.Value); next = current.ParentId.Value;
      }
    }
    foreach (var dto in snapshot.Subdivisions.Where(s => selected.Contains(s.Id)))
    {
      if (!localMap.TryGetValue(dto.Id, out var entity))
      {
        entity = new Subdivision { ExternalId = dto.Id, Name = dto.Name };
        db.Subdivisions.Add(entity); subdivisions.Add(entity); localMap.Add(dto.Id, entity); run.Added++;
      }
      else if (EntityWriteMapping.From(entity) != dto.Fields || entity.SourceUpdatedAt != SourceTime(dto.UpdatedAt) ||
        entity.ExternalParentId != dto.ParentId) run.Updated++;
      dto.Fields.ApplyTo(entity); entity.SourceUpdatedAt = SourceTime(dto.UpdatedAt);
      entity.ExternalParentId = dto.ParentId;
      Restore(entity, run);
    }
    // Получаем локальные номера новых подразделений; запись остаётся внутри общей транзакции.
    AuditSaveChangesInterceptor.ApplyAudit(db, run.ActorId);
    await db.SaveChangesAsync(ct);
    foreach (var dto in snapshot.Subdivisions.Where(s => selected.Contains(s.Id)))
    {
      var entity = localMap[dto.Id];
      var parent = dto.Id != snapshot.HeadSubdivisionId && dto.ParentId is > 0 ? localMap[dto.ParentId.Value] : null;
      entity.ParentId = parent?.Id; entity.ParentSubdivisionId = parent?.SubdivisionId;
    }
    if (snapshot.HeadSubdivisionId.HasValue && localMap.TryGetValue(snapshot.HeadSubdivisionId.Value, out var headEntity))
    {
      headEntity.ParentId = null;
      headEntity.ParentSubdivisionId = null;
    }
    BuildPaths(subdivisions);
    foreach (var dto in selectedPeople)
    {
      if (exitMarkers.TryGetValue(dto.Id, out var lastExit) && SourceTime(dto.UpdatedAt) < lastExit.EventAt)
        continue;
      var fields = dto.Fields(localMap[dto.SubdivisionId].Id);
      if (!personalMap.TryGetValue(dto.Id, out var entity))
      {
        entity = new Personal { ExternalId = dto.Id, ImportedFromApi = true };
        db.Personals.Add(entity); personalMap.Add(dto.Id, entity); run.Added++;
      }
      else if (EntityWriteMapping.From(entity) != fields || entity.SourceUpdatedAt != SourceTime(dto.UpdatedAt) ||
        entity.PersonalSubdivisionId != dto.SubdivisionId) run.Updated++;
      fields.ApplyTo(entity); entity.ImportedFromApi = true;
      entity.PersonalSubdivisionId = dto.SubdivisionId; entity.SourceUpdatedAt = SourceTime(dto.UpdatedAt);
      Restore(entity, run);
    }
    foreach (var group in snapshot.ExitEvents.GroupBy(e => e.PersonalId))
    {
      ct.ThrowIfCancellationRequested();
      var at = SourceTime(group.MaxBy(e => e.EventAt.UtcTicks)!.EventAt);
      personalMap.TryGetValue(group.Key, out var employee);
      if (run.PoolId.HasValue && (employee?.SubdivisionId is not Guid subdivisionId || !previousPool.Contains(subdivisionId))) continue;
      if (snapshot.HeadSubdivisionId.HasValue && employee?.SubdivisionId is Guid localId && !previousPool.Contains(localId) &&
          !sourcePool.Contains(employee.PersonalSubdivisionId)) continue;
      run.ExitEvents += group.Count();
      exitMarkers.TryGetValue(group.Key, out var marker);
      if (marker != null) run.RepeatedExitEvents += group.Count(e => SourceTime(e.EventAt) <= marker.EventAt);
      if (marker != null && marker.EventAt >= at)
      {
        continue;
      }
      if (marker == null)
      {
        marker = new ApiPersonnelExit { PersonalExternalId = group.Key };
        db.Add(marker); exitMarkers.Add(group.Key, marker);
      }
      marker.EventAt = at; marker.ProcessedAt = now;
      if (employee == null || !employee.ImportedFromApi) { run.UnknownExitEmployees++; continue; }
      // Более новое состояние внутри области подтверждает возвращение. Старое событие только отмечаем как обработанное.
      if (employee.SourceUpdatedAt.HasValue && employee.SourceUpdatedAt.Value > at) continue;
      Archive(employee, run);
    }
    foreach (var s in subdivisions.Where(s => !snapshot.UpdatedSince.HasValue && s.ExternalId.HasValue && previousPool.Contains(s.Id) && !remoteMap.ContainsKey(s.ExternalId.Value)))
      Archive(s, run);
    foreach (var p in personnel.Where(p => !snapshot.UpdatedSince.HasValue && p.ImportedFromApi && p.ExternalId.HasValue &&
      (!run.PoolId.HasValue && !snapshot.HeadSubdivisionId.HasValue || p.SubdivisionId.HasValue && previousPool.Contains(p.SubdivisionId.Value)) && !remotePersonnel.ContainsKey(p.ExternalId.Value)))
      Archive(p, run);
    // Дата проверки не меняет бизнес-версию и аудит записи при неизменившихся данных.
    var coveredIds = subdivisions.Where(s => s.ExternalId.HasValue &&
      (sourcePool.Contains(s.ExternalId.Value) || previousPool.Contains(s.Id))).Select(s => s.Id).ToArray();
    AuditSaveChangesInterceptor.ApplyAudit(db, run.ActorId);
    return coveredIds;
  }

  public static HashSet<long> Descendants(IReadOnlyDictionary<long, ApiSubdivision> subdivisions, long root)
  {
    var result = new HashSet<long>();
    var children = subdivisions.Values.ToLookup(s => s.ParentId);
    var queue = new Queue<long>();
    if (subdivisions.ContainsKey(root)) queue.Enqueue(root);
    while (queue.TryDequeue(out var id))
    {
      if (!result.Add(id)) throw new EntityRuleException("Дерево содержит цикл.");
      foreach (var child in children[id]) queue.Enqueue(child.Id);
    }
    return result;
  }

  // PostgreSQL хранит timestamp с точностью до микросекунд.
  private static DateTime SourceTime(DateTimeOffset value) => new(value.UtcTicks / 10 * 10, DateTimeKind.Utc);

  private static async Task AlignSequencesAsync(ApplicationDbContext db, CancellationToken ct)
  {
    // Ручные тестовые записи могут иметь явные локальные номера. Не перематываем sequence назад.
    await db.Database.ExecuteSqlRawAsync("""
        DO $sync_sequence$
        DECLARE item record; sequence_name regclass; sequence_value bigint; maximum_value bigint;
        BEGIN
          FOR item IN SELECT * FROM (VALUES ('Classifiers', 'Id'), ('Subdivisions', 'SubdivisionId'),
            ('Personals', 'PersonalId')) AS source(table_name, column_name)
          LOOP
            sequence_name := pg_get_serial_sequence(format('%I', item.table_name), item.column_name);
            EXECUTE format('SELECT last_value FROM %s', sequence_name) INTO sequence_value;
            EXECUTE format('SELECT COALESCE(MAX(%I), 0) FROM %I', item.column_name, item.table_name) INTO maximum_value;
            PERFORM setval(sequence_name, GREATEST(sequence_value, maximum_value, 1), true);
          END LOOP;
        END $sync_sequence$;
        """, ct);
  }

  private static void BuildPaths(List<Subdivision> subdivisions)
  {
    var byId = subdivisions.ToDictionary(s => s.Id);
    var paths = new Dictionary<Guid, string>();
    foreach (var node in subdivisions)
    {
      var chain = new Stack<Subdivision>(); var visited = new HashSet<Guid>(); var current = node;
      while (!paths.ContainsKey(current.Id))
      {
        if (!visited.Add(current.Id)) throw new EntityRuleException("Локальное дерево содержит цикл.");
        chain.Push(current);
        if (!current.ParentId.HasValue) break;
        if (!byId.TryGetValue(current.ParentId.Value, out current!)) throw new EntityRuleException("Отсутствует родитель локального подразделения.");
      }
      var prefix = paths.GetValueOrDefault(current.Id, "").TrimEnd('/');
      while (chain.TryPop(out var part))
      {
        prefix += "/" + part.SubdivisionId;
        paths[part.Id] = prefix.TrimEnd('/') + "/";
        prefix = paths[part.Id].TrimEnd('/');
      }
    }
    foreach (var node in subdivisions) node.Path = paths[node.Id];
  }

  private static void Archive(BaseEntity entity, SynchronizationRun run)
  {
    if (entity.Status is Status.Archived or Status.ArchivedBlocked or Status.Deleted) return;
    entity.Status = entity.Status switch {
      Status.Inserted or Status.Modified => Status.Archived,
      Status.Blocked => Status.ArchivedBlocked,
      _ => throw new EntityRuleException("Неизвестный локальный статус записи.")
    };
    if (entity is Personal p) p.ArchivedBySynchronization = true;
    if (entity is Subdivision s) s.ArchivedBySynchronization = true;
    run.Archived++;
  }

  private static void Restore(BaseEntity entity, SynchronizationRun run)
  {
    var automatic = entity is Personal p && p.ArchivedBySynchronization || entity is Subdivision s && s.ArchivedBySynchronization;
    if (!automatic || entity.Status is not (Status.Archived or Status.ArchivedBlocked)) return;
    entity.Status = entity.Status == Status.ArchivedBlocked ? Status.Blocked : Status.Modified;
    if (entity is Personal personal) personal.ArchivedBySynchronization = false;
    if (entity is Subdivision subdivision) subdivision.ArchivedBySynchronization = false;
    run.Restored++;
  }
}
