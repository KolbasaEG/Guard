using Guard.Core.Identity;
using Guard.Core.Services.DTOs;

namespace Guard.Components.Layout.Navigation;

public sealed record NavigationItem(string Text, string Path, string Icon, string? Permission = null, bool RootOnly = false)
{
  public bool IsVisible(UserAccessSnapshot? access) =>
    access != null && !string.IsNullOrEmpty(access.UserId) &&
    (!RootOnly || access.IsRoot) && (Permission == null || access.Has(Permission));
}

public sealed record NavigationGroup(string Text, IReadOnlyList<NavigationItem> Items, string Icon = "folder");

/// <summary>Навигация интерфейса. Проверки доступа в страницах и сервисах остаются обязательными.</summary>
public static class NavigationCatalog
{
  public static IReadOnlyList<NavigationGroup> Groups { get; } =
  [
    new("Обзор", [new("Главная", "/", "home")]),
    new("Организация", [
      new("Подразделения", "/subdivisions", "domain", Permissions.Subdivisions.Read),
      new("Персонал", "/personal", "group", Permissions.Personals.Read)
    ], "domain"),
    new("Доступ и безопасность", [
      new("Пользователи", "/users", "contacts", Permissions.Users.Read),
      new("Роли и полномочия", "/roles", "domino_mask", Permissions.Roles.Read),
      new("IP-адреса", "/ip", "bring_your_own_ip", Permissions.IpAddresses.Read),
      new("Политика паролей и аккаунтов", "/account-policy", "password", RootOnly: true)
    ], "shield"),
    new("Система", [
      new("Классификаторы", "/classifiers", "library_books", Permissions.Classifiers.Read),
      new("Синхронизация", "/synchronization", "sync", RootOnly: true),
      new("Журнал событий", "/Logs", "receipt_long", Permissions.Logs.Read),
      new("Журнал аудита", "/audit", "policy", Permissions.Audit.Read),
      new("Состояние и настройки аудита", "/audit/settings", "health_and_safety", RootOnly: true),
      new("Архив выгрузок", "/export-archive", "inventory_2", Permissions.Exports.ReadArchive)
    ], "settings")
  ];

  public static IEnumerable<NavigationGroup> VisibleGroups(UserAccessSnapshot? access) =>
    Groups.Select(group => new NavigationGroup(group.Text, group.Items.Where(item => item.IsVisible(access)).ToArray(), group.Icon))
      .Where(group => group.Items.Count > 0);

  public static NavigationItem? Find(string relativeUrl)
  {
    var path = "/" + relativeUrl.Split('?', '#')[0].Trim('/');
    if (path.Equals("/AdminPannel", StringComparison.OrdinalIgnoreCase)) path = "/";
    return Groups.SelectMany(group => group.Items).FirstOrDefault(item => item.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
  }
}
