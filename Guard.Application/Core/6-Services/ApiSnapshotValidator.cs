using Guard.Core.Entities;
using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

public static class ApiSnapshotValidator
{
  public static void Validate(ApiSnapshot snapshot, IEnumerable<(int Type, int Code)>? knownClassifiers = null,
    IReadOnlyDictionary<long, long?>? knownParents = null, bool allowMissingReferences = false,
    IReadOnlyDictionary<long, DateTime>? knownExitEvents = null)
  {
    var head = snapshot.HeadSubdivisionId;
    if (snapshot.ExitEvents.Any(e => e.PersonalId <= 0 || e.EventAt == default))
      throw new EntityRuleException("API вернул некорректное событие выхода сотрудника.");
    var exits = snapshot.ExitEvents.GroupBy(e => e.PersonalId).ToDictionary(g => g.Key, g => g.Max(e => e.EventAt.UtcDateTime));
    if (knownExitEvents != null)
      foreach (var (id, at) in knownExitEvents)
        if (!exits.TryGetValue(id, out var current) || at > current) exits[id] = at;
    if (snapshot.Personnel.Any(p => exits.TryGetValue(p.Id, out var at) && p.UpdatedAt.UtcTicks / 10 == at.Ticks / 10))
      throw new EntityRuleException("Неоднозначное состояние сотрудника: даты записи и события выхода совпадают. Повторите загрузку.");
    if (head is <= 0) throw new EntityRuleException("Некорректный ID головного подразделения API.");
    var classifiers = snapshot.Classifiers.Select(c => (c.Type, c.Code)).ToHashSet();
    if (classifiers.Count != snapshot.Classifiers.Count ||
        snapshot.Subdivisions.Select(s => s.Id).Distinct().Count() != snapshot.Subdivisions.Count ||
        snapshot.Personnel.Select(p => p.Id).Distinct().Count() != snapshot.Personnel.Count)
      throw new EntityRuleException("Выгрузка API содержит повторяющиеся внешние ключи.");
    if (knownClassifiers != null) classifiers.UnionWith(knownClassifiers);
    foreach (var c in snapshot.Classifiers)
      if (c.Type <= 0 || string.IsNullOrWhiteSpace(c.ClassifierName) || c.ClassifierName.Length > 256 ||
          c.Value is null || c.Value.Length > 500 || c.IsActive is not (0 or 1) || c.UpdatedAt == default)
        throw new EntityRuleException("Выгрузка содержит некорректный классификатор.");
    void CheckPair(int? type, int? code)
    {
      if (type.HasValue != code.HasValue || type.HasValue && !allowMissingReferences && !classifiers.Contains((type.Value, code!.Value)))
        throw new EntityRuleException("Выгрузка ссылается на отсутствующий классификатор.");
    }
    var subdivisions = snapshot.Subdivisions.ToDictionary(s => s.Id);
    var parents = knownParents?.ToDictionary(p => p.Key, p => p.Value) ?? new Dictionary<long, long?>();
    foreach (var s in snapshot.Subdivisions) parents[s.Id] = s.ParentId;
    if (head.HasValue && !allowMissingReferences && !parents.ContainsKey(head.Value))
      throw new EntityRuleException("Головное подразделение API отсутствует в ответе и ранее импортированных данных. Проверьте ID и дату первоначальной загрузки.");
    foreach (var s in snapshot.Subdivisions)
    {
      if (s.Id <= 0 || s.UpdatedAt == default || s.ParentId is < 0 || s.ParentId == s.Id || !allowMissingReferences && s.Id != head && s.ParentId is > 0 && !parents.ContainsKey(s.ParentId.Value))
        throw new EntityRuleException("Некорректное подразделение или отсутствующий родитель.");
      var entity = new Subdivision(); s.Fields.ApplyTo(entity); EntityInputValidation.Validate(entity);
      CheckPair(s.StatusType, s.StatusCode); CheckPair(s.OrganTypeId, s.OrganTypeCode);
      var visited = new HashSet<long>();
      var current = s.Id;
      while (true)
      {
        if (!visited.Add(current)) throw new EntityRuleException("Дерево API содержит цикл.");
        if (!parents.TryGetValue(current, out var parent) || parent is not > 0) break;
        current = parent.Value;
      }
      CheckScope(s.Id);
    }
    foreach (var p in snapshot.Personnel)
    {
      if (p.Id <= 0 || p.SubdivisionId <= 0 || p.UpdatedAt == default || !allowMissingReferences && !parents.ContainsKey(p.SubdivisionId))
        throw new EntityRuleException("Некорректный сотрудник или отсутствующее подразделение.");
      var entity = new Personal(); p.Fields(null).ApplyTo(entity); EntityInputValidation.Validate(entity);
      CheckPair(p.PersonnelCategoryType, p.PersonnelCategoryCode); CheckPair(p.SpecialRankType, p.SpecialRankCode);
      CheckPair(p.PositionType, p.PositionCode); CheckPair(p.WorkerCategoryType, p.WorkerCategoryCode);
      CheckPair(p.StatusType, p.StatusCode);
      CheckScope(p.SubdivisionId);
    }

    void CheckScope(long id)
    {
      if (!head.HasValue) return;
      var visited = new HashSet<long>();
      while (id != head.Value)
      {
        if (!visited.Add(id)) throw new EntityRuleException("Дерево API содержит цикл.");
        if (!parents.TryGetValue(id, out var parent))
        {
          if (allowMissingReferences) return;
          throw new EntityRuleException("Не удалось определить принадлежность записи головному подразделению API.");
        }
        if (parent is not > 0) throw new EntityRuleException("Выгрузка содержит записи за пределами головного подразделения API.");
        id = parent.Value;
      }
    }
  }
}
