using System.Text.Json;
using Guard.Core.Entities;
using Guard.Core.Services;
using Guard.Core.Services.Exports;

internal static class IpSearchFilterTests
{
  public static void Run(Action<bool, string> check)
  {
    var rows = new string?[] { null, "", "Alpha", "alpha", "Beta", "ALPHA" }
      .Select((description, index) => new IpAddress { Address = index.ToString(), Description = description }).AsQueryable();
    var differences = new List<string>();
    foreach (var operation in new[] { "Equals", "NotEquals", "Contains", "DoesNotContain", "StartsWith", "EndsWith",
      "IsNull", "IsNotNull", "IsEmpty", "IsNotEmpty" }) {
      foreach (var value in new string?[] { null, "", "aLPha", "a" }) {
        var condition = new ExportFilter("Description", operation, JsonSerializer.SerializeToElement(value));
        var legacy = ExportFilters.Apply(rows, condition, ExportQueries.IpFields);
        var expected = legacy.Select(p => p.Address).ToArray();
        var actual = IpSearchQuery.Filter(rows, condition).Select(p => p.Address).ToArray();
        if (!expected.SequenceEqual(actual)) differences.Add($"{operation}, value='{value ?? "<null>"}': legacy={string.Join(',', expected)} actual={string.Join(',', actual)}");
      }
    }
    foreach (var operation in new[] { "In", "NotIn" }) foreach (var values in new[] { Array.Empty<string>(), new[] { "aLPha", "Beta" } }) {
      var condition = new ExportFilter("Description", operation, JsonSerializer.SerializeToElement(values));
      var expected = ExportFilters.Apply(rows, condition, ExportQueries.IpFields).Select(p => p.Address).ToArray();
      var actual = IpSearchQuery.Filter(rows, condition).Select(p => p.Address).ToArray();
      if (!expected.SequenceEqual(actual)) differences.Add($"{operation} [{string.Join(',', values)}]: legacy={string.Join(',', expected)} actual={string.Join(',', actual)}");
    }
    check(differences.Count == 0, "IP filter compatibility matrix: " + string.Join("; ", differences));
    foreach (var operation in new[] { "In", "NotIn" }) {
      var empty = new ExportFilter("Subdivision.Name", operation);
      check(IpSearchQuery.Filter(rows, empty).Count() == rows.Count(), "unset subdivision selector leaves the applied IP scope unchanged");
    }
    foreach (var field in new[] { "Id", "Personals.Count", "Subdivision.Path", "Address.ToLower()" })
      Reject(new(field, "Equals", JsonSerializer.SerializeToElement("x")), "IP server rejects unknown fields");
    Reject(new("Address", "Custom", JsonSerializer.SerializeToElement("x")), "IP server rejects arbitrary operations");
    Reject(new("Address", "Contains", JsonSerializer.SerializeToElement(123)), "IP server rejects non-string values");
    Reject(new("Address", "In", JsonSerializer.SerializeToElement(new[] { 1, 2 })), "IP server rejects invalid collection elements");
    Reject(new(Children: Enumerable.Range(0, 101).Select(_ => new ExportFilter("Address", "IsNull")).ToArray()), "IP server bounds condition count");
    ExportFilter nested = new("Address", "IsNull");
    for (var depth = 0; depth < 18; depth++) nested = new(Children: [nested]);
    Reject(nested, "IP server bounds filter depth");

    void Reject(ExportFilter filter, string scenario) {
      try { IpSearchQuery.ValidateFilter(filter); check(false, scenario); }
      catch (ArgumentException) { check(true, scenario); }
    }
  }
}
