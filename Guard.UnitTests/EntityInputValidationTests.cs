using Guard.Core.Entities;
using Guard.Core.Services;

internal static class EntityInputValidationTests
{
  public static void Run(Action<bool, string> check)
  {
    var personal = Person();
    personal.LastName = "  Иванов  ";
    personal.FirstName = " Иван ";
    personal.MiddleName = "  Иванович ";
    personal.LastNameGen = " Иванова ";
    EntityInputValidation.Validate(personal);
    check(personal.LastName == "Иванов" && personal.FirstName == "Иван" && personal.MiddleName == "Иванович",
      "Personal required names and middle name are trimmed");
    check(personal.LastNameGen == " Иванова ", "Personal case names keep existing whitespace semantics");

    Strings(Person, EntityInputValidation.Validate,
      [("LastName", 100, true), ("FirstName", 100, true), ("MiddleName", 100, false),
       ("LastNameGen", 100, false), ("FirstNameGen", 100, false), ("MiddleNameGen", 100, false),
       ("FullName", 250, false), ("PersonalNumber", 50, false)]);
    Strings(() => new Subdivision { Name = "Подразделение" }, EntityInputValidation.Validate,
      [("Name", 500, true), ("PositionFormationName", 500, false), ("PostalCode", 20, false),
       ("Address", 1000, false), ("Phone", 50, false), ("Fax", 50, false)]);
    Strings(() => new Classifier { ClassifierName = "Справочник", Value = "Значение" }, EntityInputValidation.Validate,
      [("ClassifierName", 256, true), ("Value", 500, true)]);

    foreach (int? year in new int?[] { null, 1899, 1900, 2100, 2101 })
    {
      var entity = Person(); entity.EnlistmentYear = year;
      Accepts(() => EntityInputValidation.Validate(entity), year is null or >= 1900 and <= 2100, $"Enlistment year {year}");
    }
    foreach (var staff in new[] { -1d, 0d, 0.5d, double.MaxValue, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
      Accepts(() => EntityInputValidation.Validate(new Subdivision { Name = "Отдел", StaffCount = staff }),
        double.IsFinite(staff) && staff >= 0, $"Staff count {staff}");

    Pairs(Person, EntityInputValidation.Validate,
      [("PersonnelCategoryType", "PersonnelCategoryCode"), ("SpecialRankType", "SpecialRankCode"),
       ("PositionType", "PositionCode"), ("WorkerCategoryType", "WorkerCategoryCode"), ("StatusType", "StatusCode")]);
    Pairs(() => new Subdivision { Name = "Отдел" }, EntityInputValidation.Validate,
      [("StatusType", "StatusCode"), ("OrganTypeId", "OrganTypeCode")]);

    foreach (var current in new[] { Guid.Empty, Guid.Parse("11111111-1111-1111-1111-111111111111") })
    {
      EntityInputValidation.CheckVersion(current, current);
      check(true, "Matching version accepted including empty legacy version");
      try
      {
        EntityInputValidation.CheckVersion(current, Guid.Parse("22222222-2222-2222-2222-222222222222"));
        check(false, "Version mismatch must fail");
      }
      catch (EntityConflictException) { check(true, "Version mismatch uses conflict exception"); }
    }

    void Strings<T>(Func<T> create, Action<T> validate, (string Name, int Limit, bool Required)[] fields)
    {
      foreach (var field in fields)
      foreach (var value in new string?[] { null, "", "   ", new('я', field.Limit), new('я', field.Limit + 1) })
      {
        var entity = create();
        typeof(T).GetProperty(field.Name)!.SetValue(entity, value);
        var valid = (!field.Required || !string.IsNullOrWhiteSpace(value)) && (value?.Length ?? 0) <= field.Limit;
        Accepts(() => validate(entity), valid, $"{typeof(T).Name}.{field.Name} length {value?.Length}");
      }
      foreach (var field in fields.Where(f => f.Required))
      {
        var entity = create();
        var property = typeof(T).GetProperty(field.Name)!;
        property.SetValue(entity, " " + new string('я', field.Limit) + " ");
        validate(entity);
        check((string?)property.GetValue(entity) == new string('я', field.Limit),
          $"{typeof(T).Name}.{field.Name} trimmed before length check");
      }
    }

    void Pairs<T>(Func<T> create, Action<T> validate, (string Type, string Code)[] pairs)
    {
      foreach (var pair in pairs)
      foreach (int? type in new int?[] { null, 0, 7 })
      foreach (int? code in new int?[] { null, 0, 9 })
      {
        var entity = create();
        typeof(T).GetProperty(pair.Type)!.SetValue(entity, type);
        typeof(T).GetProperty(pair.Code)!.SetValue(entity, code);
        Accepts(() => validate(entity), type.HasValue == code.HasValue,
          $"{typeof(T).Name} complete classifier pair {pair.Type}/{pair.Code} {type}/{code}");
      }
    }

    void Accepts(Action action, bool expected, string scenario)
    {
      var accepted = true;
      try { action(); }
      catch (ArgumentException) { accepted = false; }
      check(accepted == expected, scenario);
    }
  }

  private static Personal Person() => new() { LastName = "Иванов", FirstName = "Иван" };
}
