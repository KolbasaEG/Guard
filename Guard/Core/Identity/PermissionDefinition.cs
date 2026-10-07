namespace Guard.Core.Identity;

public record PermissionDefinition(string Code, string Group, string Name, string Description,
    string? Requires = null, bool RootOnly = false);

public static class PermissionCatalog
{
  public static IReadOnlyList<PermissionDefinition> All { get; } = [
    new(Permissions.Personals.Read, "Сотрудники", "Список", "Основные сведения о сотрудниках"),
    new(Permissions.Personals.ReadDetails, "Сотрудники", "Полная карточка", "Все поля сотрудника", Permissions.Personals.Read),
    new(Permissions.Personals.ReadArchive, "Сотрудники", "Архив", "Архивные сотрудники", Permissions.Personals.Read),
    new(Permissions.Personals.Write, "Сотрудники", "Редактирование", "Создание, изменение и статусы", Permissions.Personals.ReadDetails),
    new(Permissions.Personals.Export, "Сотрудники", "Экспорт", "Выгрузка основных сведений", Permissions.Personals.Read),
    new(Permissions.Subdivisions.Read, "Подразделения", "Просмотр", "Доступная часть дерева"),
    new(Permissions.Subdivisions.ReadArchive, "Подразделения", "Архив", "Архивные подразделения", Permissions.Subdivisions.Read),
    new(Permissions.Subdivisions.Write, "Подразделения", "Редактирование", "Изменение дерева и статусов", Permissions.Subdivisions.Read),
    new(Permissions.Users.Read, "Пользователи", "Просмотр", "Учётные записи в доступных подразделениях"),
    new(Permissions.Users.Manage, "Пользователи", "Управление", "Изменение учётных записей", Permissions.Users.Read),
    new(Permissions.IpAddresses.Read, "IP-адреса", "Просмотр", "Каталог доступных IP"),
    new(Permissions.IpAddresses.Manage, "IP-адреса", "Управление", "Каталог и назначения сотрудникам", Permissions.IpAddresses.Read),
    new(Permissions.Classifiers.Read, "Классификаторы", "Просмотр", "Общий справочник"),
    new(Permissions.Classifiers.Manage, "Классификаторы", "Управление", "Изменение и синхронизация", Permissions.Classifiers.Read),
    new(Permissions.OrganTypes.Read, "Типы органов", "Просмотр", "Общий справочник"),
    new(Permissions.OrganTypes.Manage, "Типы органов", "Управление", "Изменение справочника", Permissions.OrganTypes.Read),
    new(Permissions.Logs.Read, "Логи", "Просмотр", "Первоначально только Root", RootOnly: true),
    new(Permissions.Logs.Configure, "Логи", "Настройка", "Первоначально только Root", Permissions.Logs.Read, true),
    new(Permissions.Roles.Read, "Роли", "Просмотр", "Системная операция Root", RootOnly: true),
    new(Permissions.Roles.Manage, "Роли", "Управление", "Системная операция Root", Permissions.Roles.Read, true)
  ];

  public static HashSet<string> Validate(IEnumerable<string> values)
  {
    var selected = values.ToHashSet(StringComparer.Ordinal);
    if (selected.Any(code => !All.Any(p => p.Code == code)))
      throw new ArgumentException("Выбрано неизвестное разрешение.");
    if (selected.Any(code => All.Single(p => p.Code == code).RootOnly))
      throw new ArgumentException("Системные разрешения доступны только Root.");
    foreach (var permission in All.Where(p => selected.Contains(p.Code)))
      if (permission.Requires is { } required && !selected.Contains(required))
        throw new ArgumentException($"Для «{permission.Name}» необходимо разрешение {required}.");
    return selected;
  }
}
