using System.Reflection;
using System.Text.Json;
using Guard.Core.Entities;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;

internal static class EntityWriteMappingTests
{
  public static void Run(Action<bool, string> check)
  {
    Verify<Personal, PersonalFieldsDto>(EntityWriteMapping.From, (fields, entity) => fields.ApplyTo(entity));
    Verify<Subdivision, SubdivisionFieldsDto>(EntityWriteMapping.From, (fields, entity) => fields.ApplyTo(entity));
    Verify<Classifier, ClassifierFieldsDto>(EntityWriteMapping.From, (fields, entity) => fields.ApplyTo(entity));

    void Verify<TEntity, TFields>(Func<TEntity, TFields> read, Action<TFields, TEntity> apply)
      where TEntity : new() where TFields : new()
    {
      var properties = typeof(TEntity).GetProperties().Where(p => p.SetMethod?.IsPublic == true && Scalar(p.PropertyType)).ToArray();
      var editable = typeof(TFields).GetProperties();
      var editableNames = editable.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
      var entity = new TEntity();
      var fields = new TFields();
      foreach (var property in properties) property.SetValue(entity, Sample(property.PropertyType, 1));
      foreach (var property in editable) property.SetValue(fields, Sample(property.PropertyType, 2));
      var original = properties.ToDictionary(p => p.Name, p => p.GetValue(entity));
      var navigations = typeof(TEntity).GetProperties().Where(p => !Scalar(p.PropertyType)).ToArray();
      foreach (var property in navigations.Where(p => p.SetMethod?.IsPublic == true && p.PropertyType.GetConstructor(Type.EmptyTypes) != null))
        property.SetValue(entity, Activator.CreateInstance(property.PropertyType));
      var originalNavigations = navigations.ToDictionary(p => p.Name, p => p.GetValue(entity));

      var initialFields = read(entity);
      foreach (var property in editable)
        check(Equals(property.GetValue(initialFields), original[property.Name]), $"{typeof(TEntity).Name} read mapping preserves {property.Name}");
      check(properties.All(p => Equals(p.GetValue(entity), original[p.Name])), $"{typeof(TEntity).Name} read mapping does not mutate entity");

      apply(fields, entity);
      foreach (var property in properties) {
        var expected = editableNames.Contains(property.Name) ? typeof(TFields).GetProperty(property.Name)!.GetValue(fields) : original[property.Name];
        check(Equals(property.GetValue(entity), expected), $"{typeof(TEntity).Name} write mapping respects editable boundary {property.Name}");
      }
      check(EqualityComparer<TFields>.Default.Equals(read(entity), fields), $"{typeof(TEntity).Name} complete field roundtrip");

      var empty = new TFields();
      apply(empty, entity);
      foreach (var property in editable)
        check(Equals(typeof(TEntity).GetProperty(property.Name)!.GetValue(entity), property.GetValue(empty)), $"{typeof(TEntity).Name} empty values clear {property.Name}");
      check(properties.Where(p => !editableNames.Contains(p.Name)).All(p => Equals(p.GetValue(entity), original[p.Name])),
        $"{typeof(TEntity).Name} clearing editable fields preserves protected metadata");
      check(navigations.All(p => ReferenceEquals(p.GetValue(entity), originalNavigations[p.Name])),
        $"{typeof(TEntity).Name} mapping preserves navigation and collection references");

      var json = JsonSerializer.Serialize(fields);
      check(EqualityComparer<TFields>.Default.Equals(JsonSerializer.Deserialize<TFields>(json), fields), $"{typeof(TFields).Name} JSON roundtrip");
      check(JsonSerializer.Deserialize<JsonElement>(json).EnumerateObject().Select(p => p.Name).ToHashSet().SetEquals(editableNames),
        $"{typeof(TFields).Name} JSON contains editable properties only");
      check(editable.All(p => Scalar(p.PropertyType)), $"{typeof(TFields).Name} properties have no entity dependency");
      var declaredMethods = typeof(TFields).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
      check(declaredMethods.All(m => m.ReturnType != typeof(TEntity) && m.GetParameters().All(p => p.ParameterType != typeof(TEntity))),
        $"{typeof(TFields).Name} public methods have no entity dependency");
    }
  }

  private static bool Scalar(Type type) => type == typeof(string) || (Nullable.GetUnderlyingType(type) ?? type).IsValueType;

  private static object Sample(Type type, int seed)
  {
    type = Nullable.GetUnderlyingType(type) ?? type;
    if (type == typeof(string)) return $"sample-{seed}";
    if (type == typeof(Guid)) return Guid.Parse(seed == 1 ? "11111111-1111-1111-1111-111111111111" : "22222222-2222-2222-2222-222222222222");
    if (type == typeof(DateTime)) return new DateTime(2026, 1, seed, 0, 0, 0, DateTimeKind.Utc);
    if (type == typeof(bool)) return seed == 1;
    if (type.IsEnum) return Enum.ToObject(type, seed);
    return Convert.ChangeType(seed, type);
  }
}
