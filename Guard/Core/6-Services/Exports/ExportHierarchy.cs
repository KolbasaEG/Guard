namespace Guard.Core.Services.Exports;

public sealed record ExportSubdivision(Guid Id, Guid? ParentId, long? PathId, string Name, string Path);
public sealed class ExportHierarchy(IEnumerable<ExportSubdivision> subdivisions)
{
  private readonly Dictionary<Guid, ExportSubdivision> nodes = subdivisions.ToDictionary(s => s.Id);
  private readonly Dictionary<Guid, string[]> cache = [];
  public string[] Get(Guid? id)
  {
    if (id == null) return [];
    if (cache.TryGetValue(id.Value, out var known)) return known;
    var chain = new List<ExportSubdivision>(); var visited = new HashSet<Guid>(); var current = id;
    while (current != null) {
      if (!visited.Add(current.Value) || chain.Count >= 64 || !nodes.TryGetValue(current.Value, out var node))
        throw new InvalidOperationException("Иерархия подразделений повреждена.");
      chain.Add(node); current = node.ParentId;
    }
    chain.Reverse();
    foreach (var node in chain) {
      if (!ExportAccess.ValidPath(node.Path)) throw new InvalidOperationException("Путь подразделения повреждён.");
      if (node.ParentId is { } parent && (!nodes.TryGetValue(parent, out var p) || node.Path != p.Path + node.PathId + "/"))
        throw new InvalidOperationException("Родитель и путь подразделения не согласованы.");
      if (!node.PathId.HasValue || node.ParentId == null && node.Path != $"/{node.PathId}/")
        throw new InvalidOperationException("Ключ подразделения и путь не согласованы.");
    }
    return cache[id.Value] = chain.Select(n => n.Name).ToArray();
  }
}
