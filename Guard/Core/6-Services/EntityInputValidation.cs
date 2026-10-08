using Guard.Core.Entities;

namespace Guard.Core.Services;

internal static class EntityInputValidation
{
  private static string Required(string? value, int length, string label)
  {
    value = value?.Trim();
    if (string.IsNullOrEmpty(value)) throw new ArgumentException($"Заполните поле «{label}».");
    Limit(value, length, label);
    return value;
  }
  private static void Limit(string? value, int length, string label)
  {
    if (value?.Length > length) throw new ArgumentException($"Поле «{label}» не должно превышать {length} символов.");
  }
  public static void Validate(Personal entity)
  {
    entity.LastName = Required(entity.LastName, 100, "Фамилия");
    entity.FirstName = Required(entity.FirstName, 100, "Имя");
    entity.MiddleName = entity.MiddleName?.Trim();
    foreach (var value in new[] { entity.MiddleName, entity.LastNameGen, entity.FirstNameGen, entity.MiddleNameGen }) Limit(value, 100, "ФИО");
    Limit(entity.FullName, 250, "Фамилия и инициалы");
    Limit(entity.PersonalNumber, 50, "Личный номер");
    ValidatePair(entity.PersonnelCategoryType, entity.PersonnelCategoryCode);
    ValidatePair(entity.SpecialRankType, entity.SpecialRankCode);
    ValidatePair(entity.PositionType, entity.PositionCode);
    ValidatePair(entity.WorkerCategoryType, entity.WorkerCategoryCode);
    ValidatePair(entity.StatusType, entity.StatusCode);
  }
  public static void Validate(Subdivision entity)
  {
    entity.Name = Required(entity.Name, 500, "Наименование");
    Limit(entity.PositionFormationName, 500, "Наименование для должности");
    Limit(entity.PostalCode, 20, "Почтовый индекс");
    Limit(entity.Address, 1000, "Адрес");
    Limit(entity.Phone, 50, "Телефон");
    Limit(entity.Fax, 50, "Факс");
    if (!double.IsFinite(entity.StaffCount) || entity.StaffCount < 0) throw new ArgumentException("Штатная численность должна быть неотрицательным числом.");
    ValidatePair(entity.StatusType, entity.StatusCode);
    ValidatePair(entity.OrganTypeId, entity.OrganTypeCode);
  }
  public static void Validate(Classifier entity)
  {
    entity.ClassifierName = Required(entity.ClassifierName, 256, "Наименование справочника");
    entity.Value = Required(entity.Value, 500, "Значение");
  }
  public static void Validate(OrganType entity) => entity.Name = Required(entity.Name, 500, "Наименование");
  private static void ValidatePair(int? type, int? code)
  {
    if (type.HasValue != code.HasValue) throw new ArgumentException("Выберите справочное значение целиком.");
  }

  public static void CheckVersion(Guid current, Guid expected)
  {
    if (current != expected) throw new EntityConflictException();
  }
}
