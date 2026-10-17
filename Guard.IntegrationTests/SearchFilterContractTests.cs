using System.Text.Json;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Services;
using Guard.Core.Services.Exports;
using Microsoft.EntityFrameworkCore;

internal static class SearchFilterContractTests
{
  public static void Run(Action<bool, string> check)
  {
    Compare<ApplicationUser>(UserSearchFilter.Types, UserSearchFilter.Validate);
    Compare<ApplicationRole>(RoleSearchFilter.Types, RoleSearchFilter.Validate);
    Compare<LogEntry>(LogSearchFilter.Types, LogSearchFilter.Validate);
    Compare<IpAddress>(IpSearchFilter.Types, IpSearchFilter.Validate,
      filter => _ = IpSearchQuery.Filter(Array.Empty<IpAddress>().AsQueryable(), filter));
    Compare<Subdivision>(SubdivisionSearchFilter.Types, SubdivisionSearchFilter.Validate,
      filter => _ = SubdivisionSearchQuery.Filter(Array.Empty<Subdivision>().AsQueryable(), filter));
    Compare<Classifier>(ClassifierSearchFilter.Types, ClassifierSearchFilter.Validate,
      filter => _ = ClassifierSearchQuery.Filter(Array.Empty<Classifier>().AsQueryable(), filter));

    var users = new[] {
      new ApplicationUser { Id = "1", UserName = null, PersonalId = null },
      new ApplicationUser { Id = "2", UserName = "", MustChangePassword = true },
      new ApplicationUser { Id = "3", UserName = "Alpha", PersonalId = Guid.Parse("11111111-1111-1111-1111-111111111111") },
      new ApplicationUser { Id = "4", UserName = "alpha" },
      new ApplicationUser { Id = "5", UserName = "Beta" }
    }.AsQueryable();
    foreach (var (operation, json, expected) in new[] {
      ("Equals", "\"ALPHA\"", "3,4"), ("NotEquals", "\"alpha\"", "1,2,5"),
      ("Contains", "\"ALP\"", "3,4"), ("DoesNotContain", "\"alp\"", "1,2,5"),
      ("StartsWith", "\"al\"", "3,4"), ("EndsWith", "\"HA\"", "3,4"),
      ("In", "[\"Alpha\"]", "3"), ("NotIn", "[\"Alpha\"]", "1,2,4,5"),
      ("IsNull", "123", "1"), ("IsNotNull", "null", "2,3,4,5"),
      ("IsEmpty", "null", "2"), ("IsNotEmpty", "null", "1,3,4,5"),
      ("Equals", "\"\"", "1,2,3,4,5"), ("In", "null", "1,2,3,4,5") }) {
      var filter = new ExportFilter("UserName", operation, Json(json));
      UserSearchFilter.Validate(filter);
      check(string.Join(',', UserSearchQuery.Filter(users, filter).Select(u => u.Id)) == expected, $"search string semantics {operation}/{json}");
    }
    check(UserSearchQuery.Filter(users, new("PersonalId", "IsNull")).Count() == 4, "nullable GUID null filter");
    check(UserSearchQuery.Filter(users, new("MustChangePassword", "Equals", Json("true"))).Single().Id == "2", "boolean filter");
    var group = new ExportFilter(All: false, Children: [new("UserName", "Equals", Json("\"Beta\"")), new("MustChangePassword", "Equals", Json("true"))]);
    check(string.Join(',', UserSearchQuery.Filter(users, group).Select(u => u.Id)) == "2,5", "OR search group");
    check(UserSearchQuery.Filter(users, new(Children: [])).Count() == 5, "empty group is unrestricted");

    using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
    check(UserSearchQuery.Filter(db.Users, new("LastActivityAtUtc", "GreaterThan", Json("\"2026-01-01T00:00:00Z\""))).ToQueryString().Contains("LastActivityAtUtc"), "UTC date offset filter translates to PostgreSQL");
    check(RoleSearchQuery.Sort(RoleSearchQuery.Filter(db.Roles, new("Name", "Contains", Json("\"root\""))), "Name desc").ToQueryString().Contains("ORDER BY"), "role filter and stable sorting translate to PostgreSQL");
    check(IpSearchQuery.Sort(IpSearchQuery.Filter(db.Set<IpAddress>(), new("Subdivision.Name", "Contains", Json("\"alpha\""))), "Subdivision.Name asc")
      .ToQueryString().Contains("LEFT JOIN"), "IP nested filter and sorting translate to PostgreSQL");
    check(SubdivisionSearchQuery.Sort(SubdivisionSearchQuery.Filter(db.Set<Subdivision>(), new("StaffCount", "GreaterThan", Json("10.5"))), "Parent.Name desc")
      .ToQueryString().Contains("ORDER BY"), "subdivision numeric filter and nested sorting translate to PostgreSQL");
    check(ClassifierSearchQuery.Filter(db.Set<Classifier>(), new("UpdatedAt", "GreaterThan", Json("\"2026-01-01T00:00:00Z\"")))
      .ToQueryString().Contains("UpdatedAt"), "classifier UTC filter translates to PostgreSQL");
    var ips = new[] {
      new IpAddress { Address = "192.0.2.1", Subdivision = null },
      new IpAddress { Address = "192.0.2.2", Subdivision = new() { Name = "Alpha" } }
    }.AsQueryable();
    check(IpSearchQuery.Filter(ips, new("Subdivision.Name", "IsNull")).Single().Address == "192.0.2.1", "missing IP subdivision matches nested null filter");
    check(IpSearchQuery.Filter(ips, new("Subdivision.Name", "Equals", Json("\"ALPHA\""))).Single().Address == "192.0.2.2", "nested IP text comparison ignores case");
    foreach (var value in new[] { "1e999", "\"NaN\"", "\"Infinity\"" })
      check(!Accepts(() => SubdivisionSearchFilter.Validate(new("StaffCount", "Equals", Json(value)))), "subdivision rejects nonfinite numeric input");
    check(!Accepts(() => ClassifierSearchFilter.Validate(new("Code", "Equals", Json("2147483648")))), "classifier rejects integer overflow");
    check(!Accepts(() => IpSearchQuery.ValidateFilter(new("Unknown", "Equals"))), "IP server validator uses independent field restrictions");
    check(!Accepts(() => SubdivisionSearchQuery.ValidateFilter(new("Unknown", "Equals"))), "subdivision server validator uses independent field restrictions");
    check(!Accepts(() => ClassifierSearchQuery.ValidateFilter(new("Unknown", "Equals"))), "classifier server validator uses independent field restrictions");

    void Compare<T>(IReadOnlyDictionary<string, Type> types, Action<ExportFilter?> validate, Action<ExportFilter?>? compile = null)
    {
      var fields = types.Keys.ToHashSet(StringComparer.Ordinal);
      // Keep the comparison against the entity expression compiler, not the forwarding validator.
      void queryValidate(ExportFilter? filter) {
        if (compile != null) compile(filter);
        else _ = RestrictedSearchQuery<T>.Filter(Array.Empty<T>().AsQueryable(), filter, fields);
      }
      Type FieldType(string path) {
        var type = typeof(T);
        foreach (var part in path.Split('.')) type = type.GetProperty(part)!.PropertyType;
        return type;
      }
      string[] operations = ["Equals", "NotEquals", "Contains", "DoesNotContain", "StartsWith", "EndsWith", "In", "NotIn",
        "IsNull", "IsNotNull", "IsEmpty", "IsNotEmpty", "GreaterThan", "GreaterThanOrEquals", "LessThan", "LessThanOrEquals", "Custom", "", null!];
      string[] values = ["null", "\"\"", "\"alpha\"", "true", "42", "[]", "[\"Alpha\",\"Beta\"]", "[null]", "[1]", "{}",
        "\"11111111-1111-1111-1111-111111111111\"", "\"2026-01-01T00:00:00Z\"", "\"2026-01-01T03:00:00+03:00\"", "\"2026-01-01T00:00:00\""];
      foreach (var field in fields.Concat(new[] { "Unknown", "Id.ToString()", "", " " }))
        foreach (var operation in operations)
          foreach (var value in values) {
            var filter = new ExportFilter(field, operation, Json(value));
            check(Accepts(() => validate(filter)) == Accepts(() => queryValidate(filter)), $"{typeof(T).Name} filter contract parity {field}/{operation}/{value}");
          }
      foreach (var field in fields) {
        var fieldType = FieldType(field);
        check(fieldType == types[field], $"{typeof(T).Name} independent field type matches storage {field}");
        var sample = fieldType == typeof(string) ? Json("\"a\"") : fieldType == typeof(bool) ? Json("true") :
          fieldType == typeof(Guid?) || fieldType == typeof(Guid) ? Json("\"11111111-1111-1111-1111-111111111111\"") :
          fieldType == typeof(int) || fieldType == typeof(double) ? Json("42") : Json("\"2026-01-01T00:00:00Z\"");
        check(Accepts(() => validate(new(field, "Equals", sample))), $"{typeof(T).Name} supported field accepts matching value {field}");
        check(!Accepts(() => validate(new(field, "Equals", Json("{}")))) , $"{typeof(T).Name} field rejects object value {field}");
      }
      foreach (var nodes in new[] { 99, 100 }) {
        var filter = new ExportFilter(Children: Enumerable.Range(0, nodes).Select(_ => new ExportFilter()).ToArray());
        check(Accepts(() => validate(filter)) == (nodes == 99), "filter node limit includes root");
        check(Accepts(() => validate(filter)) == Accepts(() => queryValidate(filter)), "filter node limit parity");
      }
      foreach (var depth in new[] { 16, 17 }) {
        ExportFilter filter = new();
        for (var i = 0; i < depth; i++) filter = new(Children: [filter]);
        check(Accepts(() => validate(filter)) == (depth == 16), "filter depth boundary");
        check(Accepts(() => validate(filter)) == Accepts(() => queryValidate(filter)), "filter depth parity");
      }
      check(!Accepts(() => validate(new(Children: [null!]))), "null filter child rejected");
      foreach (var size in new[] { 1024, 1025 }) {
        var filter = new ExportFilter(fields.First(f => FieldType(f) == typeof(string)), "In", JsonSerializer.SerializeToElement(Enumerable.Repeat("a", size)));
        check(Accepts(() => validate(filter)) == (size == 1024), "string collection size boundary");
        check(Accepts(() => validate(filter)) == Accepts(() => queryValidate(filter)), "string collection size parity");
      }
    }
  }

  private static JsonElement Json(string value) => JsonSerializer.Deserialize<JsonElement>(value);
  private static bool Accepts(Action action)
  {
    try { action(); return true; }
    catch (ArgumentException) { return false; }
  }
}
