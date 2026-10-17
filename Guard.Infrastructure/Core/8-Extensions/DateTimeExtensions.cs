using System.Reflection;

namespace Guard.Core.Extensions
{
  public static class DateTimeExtensions
  {
    private static readonly Dictionary<Type, PropertyInfo[]> PropertyCache = new();

    public static T ConvertDateTimesToLocal<T>(this T item, TimeZoneInfo? targetTimeZone = null) where T : class
    {
      if (item == null) return item;

      // По умолчанию берем локальное время сервера или нужный часовой пояс (например, MSK)
      var tz = targetTimeZone ?? TimeZoneInfo.Local;
      var type = typeof(T);

      if (!PropertyCache.TryGetValue(type, out var properties))
      {
        properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => (p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTime?)) && p.SetMethod != null)
            .ToArray();

        PropertyCache[type] = properties;
      }

      foreach (var prop in properties)
      {
        if (prop.PropertyType == typeof(DateTime))
        {
          var val = (DateTime)prop.GetValue(item)!;
          if (val != default)
          {
            var utc = val.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(val, DateTimeKind.Utc) : val.ToUniversalTime();
            prop.SetValue(item, TimeZoneInfo.ConvertTimeFromUtc(utc, tz));
          }
        }
        else if (prop.PropertyType == typeof(DateTime?))
        {
          var val = (DateTime?)prop.GetValue(item);
          if (val.HasValue && val.Value != default)
          {
            var utc = val.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(val.Value, DateTimeKind.Utc) : val.Value.ToUniversalTime();
            prop.SetValue(item, TimeZoneInfo.ConvertTimeFromUtc(utc, tz));
          }
        }
      }

      return item;
    }

    public static List<T> ConvertDateTimesToLocal<T>(this List<T> items, TimeZoneInfo? targetTimeZone = null) where T : class
    {
      if (items == null) return new List<T>();
      foreach (var item in items)
      {
        item.ConvertDateTimesToLocal(targetTimeZone);
      }
      return items;
    }
  }
}
