using System.Globalization;
using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

// Only identifiers, field names and violation codes leave the analyzer; never field values.
public static class ApiDiagnosticAnalyzer
{
  public static IReadOnlyList<ApiDiagnosticIssue> Analyze(ApiSnapshot snapshot,
    IReadOnlyDictionary<long, long?> localParents, IReadOnlySet<(int Type, int Code)> localClassifiers,
    CancellationToken ct = default)
  {
    var issues = new List<ApiDiagnosticIssue>();
    void Add(string resource, string id, string field, string code, string? reference = null, bool local = false)
      => issues.Add(new(resource, id, field, code, reference, local));
    static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);
    static string Key(int type, int code) => $"{type}:{code}";
    var parents = snapshot.Subdivisions.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First().ParentId);
    var classifiers = snapshot.Classifiers.Select(c => (c.Type, c.Code)).ToHashSet();
    foreach (var e in snapshot.ExitEvents)
    {
      ct.ThrowIfCancellationRequested();
      if (e.PersonalId <= 0) Add("personnel/out-of-scope", Id(e.PersonalId), "PersonalId", "InvalidId");
      if (e.EventAt == default) Add("personnel/out-of-scope", Id(e.PersonalId), "EventAt", "MissingDate");
    }
    var exitDates = snapshot.ExitEvents.GroupBy(e => e.PersonalId).ToDictionary(g => g.Key, g => g.Max(e => e.EventAt.UtcTicks / 10));
    foreach (var p in snapshot.Personnel)
      if (exitDates.TryGetValue(p.Id, out var at) && at == p.UpdatedAt.UtcTicks / 10)
        Add("personnel", Id(p.Id), "UpdatedAt/EventAt", "AmbiguousExitState");

    foreach (var group in snapshot.Subdivisions.GroupBy(s => s.Id).Where(g => g.Count() > 1))
      Add("subdivisions", Id(group.Key), "Id", "DuplicateId");
    foreach (var group in snapshot.Personnel.GroupBy(p => p.Id).Where(g => g.Count() > 1))
      Add("personnel", Id(group.Key), "Id", "DuplicateId");
    foreach (var group in snapshot.Classifiers.GroupBy(c => (c.Type, c.Code)).Where(g => g.Count() > 1))
      Add("classifiers", Key(group.Key.Type, group.Key.Code), "Type/Code", "DuplicateKey");

    void Text(string resource, string id, string field, string? value, int limit, bool required = false, bool trim = true)
    {
      if (trim) value = value?.Trim();
      if (required && string.IsNullOrWhiteSpace(value)) Add(resource, id, field, "Required");
      if (value?.Length > limit) Add(resource, id, field, "TooLong");
    }
    void Pair(string resource, string id, string field, int? type, int? code)
    {
      if (type.HasValue != code.HasValue) Add(resource, id, field, "IncompletePair");
      else if (type.HasValue && !classifiers.Contains((type.Value, code!.Value)))
        Add(resource, id, field, "MissingClassifierInResponse", Key(type.Value, code.Value), localClassifiers.Contains((type.Value, code.Value)));
    }
    foreach (var c in snapshot.Classifiers)
    {
      ct.ThrowIfCancellationRequested();
      var id = Key(c.Type, c.Code);
      if (c.Type <= 0) Add("classifiers", id, "Type", "InvalidId");
      Text("classifiers", id, "ClassifierName", c.ClassifierName, 256, true, false);
      Text("classifiers", id, "Value", c.Value, 500, trim: false);
      if (c.Value == null) Add("classifiers", id, "Value", "Required");
      if (c.IsActive is not (0 or 1)) Add("classifiers", id, "IsActive", "InvalidState");
      if (c.UpdatedAt == default) Add("classifiers", id, "UpdatedAt", "Required");
    }
    foreach (var s in snapshot.Subdivisions)
    {
      ct.ThrowIfCancellationRequested();
      var id = Id(s.Id);
      if (s.Id <= 0) Add("subdivisions", id, "Id", "InvalidId");
      if (s.ParentId is < 0) Add("subdivisions", id, "ParentId", "InvalidId", Id(s.ParentId.Value));
      if (s.ParentId == s.Id) Add("subdivisions", id, "ParentId", "SelfReference", id);
      if (s.Id != snapshot.HeadSubdivisionId && s.ParentId is > 0 && !parents.ContainsKey(s.ParentId.Value))
        Add("subdivisions", id, "ParentId", "MissingParentInResponse", Id(s.ParentId.Value), localParents.ContainsKey(s.ParentId.Value));
      if (s.UpdatedAt == default) Add("subdivisions", id, "UpdatedAt", "Required");
      Text("subdivisions", id, "Name", s.Name, 500, true);
      Text("subdivisions", id, "PositionFormationName", s.PositionFormationName, 500, trim: false);
      Text("subdivisions", id, "PostalCode", s.PostalCode, 20, trim: false);
      Text("subdivisions", id, "Address", s.Address, 1000, trim: false);
      Text("subdivisions", id, "Phone", s.Phone, 50, trim: false);
      Text("subdivisions", id, "Fax", s.Fax, 50, trim: false);
      if (!double.IsFinite(s.StaffCount) || s.StaffCount < 0) Add("subdivisions", id, "StaffCount", "InvalidNumber");
      Pair("subdivisions", id, "StatusType/StatusCode", s.StatusType, s.StatusCode);
      Pair("subdivisions", id, "OrganTypeId/OrganTypeCode", s.OrganTypeId, s.OrganTypeCode);
    }
    foreach (var p in snapshot.Personnel)
    {
      ct.ThrowIfCancellationRequested();
      var id = Id(p.Id);
      if (p.Id <= 0) Add("personnel", id, "Id", "InvalidId");
      if (p.SubdivisionId <= 0) Add("personnel", id, "SubdivisionId", "InvalidId", Id(p.SubdivisionId));
      else if (!parents.ContainsKey(p.SubdivisionId))
        Add("personnel", id, "SubdivisionId", "MissingSubdivisionInResponse", Id(p.SubdivisionId), localParents.ContainsKey(p.SubdivisionId));
      if (p.UpdatedAt == default) Add("personnel", id, "UpdatedAt", "Required");
      Text("personnel", id, "LastName", p.LastName, 100, true);
      Text("personnel", id, "FirstName", p.FirstName, 100, true);
      Text("personnel", id, "MiddleName", p.MiddleName, 100);
      Text("personnel", id, "LastNameGen", p.LastNameGen, 100, trim: false);
      Text("personnel", id, "FirstNameGen", p.FirstNameGen, 100, trim: false);
      Text("personnel", id, "MiddleNameGen", p.MiddleNameGen, 100, trim: false);
      Text("personnel", id, "FullName", p.FullName, 250, trim: false);
      Text("personnel", id, "PersonalNumber", p.PersonalNumber, 50, trim: false);
      if (p.EnlistmentYear is < 1900 or > 2100) Add("personnel", id, "EnlistmentYear", "OutOfRange");
      Pair("personnel", id, "PersonnelCategoryType/Code", p.PersonnelCategoryType, p.PersonnelCategoryCode);
      Pair("personnel", id, "SpecialRankType/Code", p.SpecialRankType, p.SpecialRankCode);
      Pair("personnel", id, "PositionType/Code", p.PositionType, p.PositionCode);
      Pair("personnel", id, "WorkerCategoryType/Code", p.WorkerCategoryType, p.WorkerCategoryCode);
      Pair("personnel", id, "StatusType/Code", p.StatusType, p.StatusCode);
    }
    // Check the response and then the graph used by incremental import, including known parents.
    Cycles(parents, "CycleInResponse");
    var combined = localParents.ToDictionary(p => p.Key, p => p.Value);
    foreach (var pair in parents) combined[pair.Key] = pair.Value;
    Cycles(combined, "CycleWithLocalData");
    if (snapshot.HeadSubdivisionId.HasValue)
    {
      var head = snapshot.HeadSubdivisionId.Value;
      if (!combined.ContainsKey(head)) Add("subdivisions", Id(head), "Id", "MissingConfiguredHead");
      foreach (var id in parents.Keys.Concat(snapshot.Personnel.Select(p => p.SubdivisionId)).Distinct())
      {
        ct.ThrowIfCancellationRequested();
        var visited = new HashSet<long>(); var current = id;
        while (current != head && visited.Add(current) && combined.TryGetValue(current, out var parent))
        {
          if (parent is not > 0)
          { Add("subdivisions", Id(id), "ParentId", "OutsideConfiguredHead", Id(head)); break; }
          current = parent.Value;
        }
      }
    }
    return issues;

    void Cycles(IReadOnlyDictionary<long, long?> graph, string code)
    {
      var finished = new HashSet<long>();
      foreach (var start in graph.Keys.Order())
      {
        ct.ThrowIfCancellationRequested();
        if (finished.Contains(start)) continue;
        var chain = new List<long>(); var positions = new Dictionary<long, int>(); var current = start;
        while (!finished.Contains(current) && graph.ContainsKey(current))
        {
          ct.ThrowIfCancellationRequested();
          if (positions.TryGetValue(current, out var position))
          {
            foreach (var node in chain.Skip(position)) Add("subdivisions", Id(node), "ParentId", code, Id(graph[node]!.Value));
            break;
          }
          positions[current] = chain.Count; chain.Add(current);
          if (graph[current] is not > 0) break;
          current = graph[current]!.Value;
        }
        finished.UnionWith(chain);
      }
    }
  }
}
