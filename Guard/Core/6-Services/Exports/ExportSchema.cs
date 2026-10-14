using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Guard.Core.Enums;

namespace Guard.Core.Services.Exports;

public static class ExportSchema
{
  private static readonly string[] PersonalFields = ["LastName", "FirstName", "MiddleName", "Subdivision", "Status"];
  private static readonly string[] SubdivisionFields = ["Name", "Parent", "OrganType", "Status", "PositionFormationName", "PostalCode", "Address", "Phone", "StaffCount", "LevelOrder", "IsDepartment"];
  private static readonly string[] IpFields = ["Address", "Description", "Subdivision", "Status"];
  public static string Title(ExportArea area) => area switch {
    ExportArea.Personals => "Сотрудники", ExportArea.Subdivisions => "Подразделения", ExportArea.IpAddresses => "IP-адреса",
    _ => throw new ArgumentException("Неизвестная область экспорта.")
  };
  public static string Permission(ExportArea area) => area switch {
    ExportArea.Personals => Identity.Permissions.Personals.Export,
    ExportArea.Subdivisions => Identity.Permissions.Subdivisions.Export,
    ExportArea.IpAddresses => Identity.Permissions.IpAddresses.Export,
    _ => throw new ArgumentException("Неизвестная область экспорта.")
  };
  public static PropertyInfo[] Properties(ExportArea area)
  {
    var (type, fields) = area switch {
      ExportArea.Personals => (typeof(PersonalExportDto), PersonalFields),
      ExportArea.Subdivisions => (typeof(SubdivisionExportDto), SubdivisionFields),
      ExportArea.IpAddresses => (typeof(IpAddressExportDto), IpFields),
      _ => throw new ArgumentException("Неизвестная область экспорта.")
    };
    return fields.Select(name => type.GetProperty(name) ?? throw new InvalidOperationException("Поле DTO не найдено.")).ToArray();
  }
  public static string[] Headers(ExportArea area) => Properties(area).Select(p =>
    p.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? throw new InvalidOperationException($"У {p.Name} отсутствует DisplayName.")).ToArray();
  public static ExportCell[] Cells(ExportArea area, object dto) => Properties(area).Select(p => {
    var value = p.GetValue(dto);
    return value switch {
      null => new ExportCell(""), bool b => new ExportCell(b ? "Да" : "Нет", Boolean: b),
      int n => new ExportCell(n.ToString(CultureInfo.InvariantCulture), n),
      double n when double.IsFinite(n) => new ExportCell(n.ToString(CultureInfo.InvariantCulture), n),
      _ => new ExportCell(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "")
    };
  }).ToArray();
  public static string StatusName(Status status) => status switch {
    Status.Inserted => "Добавлено", Status.Modified => "Изменено", Status.Blocked => "Заблокировано",
    Status.Archived => "Архив", Status.ArchivedBlocked => "Архив, заблокировано", Status.Deleted => "Удалено",
    _ => throw new ArgumentException("Неизвестный статус.")
  };
}
