using Guard.Core.Contexts;
using Guard.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Guard;

/// <summary>
/// Сидер для начальных данных в Development окружении.
/// 
/// Создаёт:
/// - Роли (Administrator, Manager, Employee)
/// - Администратора системы
/// - Корневое подразделение
/// 
/// Запускается только при app.Environment.IsDevelopment() в Program.cs
/// </summary>
public static class DatabaseSeeder
{
  public static async Task SeedAsync(
      ApplicationDbContext context,
      UserManager<ApplicationUser> userManager,
      RoleManager<ApplicationRole> roleManager)
  {
    // Применяем миграции автоматически в Development (удобно)
    await context.Database.MigrateAsync();

    // === 1. Создание ролей ===
    await EnsureRoleAsync(roleManager, "Root");
    await EnsureRoleAsync(roleManager, "Администратор");

    // === 2. Создание администратора ===
    const string adminEmail = "admin@guard.local";
    const string adminPassword = "Admin123!";

    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser == null)
    {
      adminUser = new ApplicationUser
      {
        UserName = adminEmail,
        Email = adminEmail,
        EmailConfirmed = true
      };

      var result = await userManager.CreateAsync(adminUser, adminPassword);
      if (result.Succeeded)
      {
        await userManager.AddToRoleAsync(adminUser, "Root");
        await userManager.AddToRoleAsync(adminUser, "Администратор");
      }
    }

    await SeedSubdivisionsAsync(context);
  }

  private static async Task EnsureRoleAsync(RoleManager<ApplicationRole> roleManager, string roleName)
  {
    if (!await roleManager.RoleExistsAsync(roleName))
    {
      await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
    }
  }
  /// <summary>
  /// Добавление иерархии подразделений из Subdivision_4.xlsx
  /// </summary>
  private static async Task SeedSubdivisionsAsync(ApplicationDbContext context)
  {
    if (await context.Subdivisions.AnyAsync())
    {
      return;
    }

    // Уровень 1: Корневой узел
    var sub_1 = new Subdivision
    {
      Id = Guid.NewGuid(),
      SubdivisionId = 1,
      ParentId = null,
      Name = "Департамент охраны",
      PositionFormationName = "Департамент охраны",
      IsDepartment = false,
      LevelOrder = 1,
      Path = "/1/"
    };

    // Уровень 2: Региональное управление
    var sub_2 = new Subdivision
    {
      Id = Guid.NewGuid(),
      SubdivisionId = 2,
      ParentId = sub_1.Id,
      Name = "Минское городское управление",
      PositionFormationName = "Минское городское управление",
      IsDepartment = false,
      LevelOrder = 1,
      Path = "/1/2/"
    };

    // Уровень 3: Отделы и территориальные подразделения
    var sub_3 = new Subdivision
    {
      Id = Guid.NewGuid(),
      SubdivisionId = 3,
      ParentId = sub_2.Id,
      Name = "Центральный (г. Минска) отдел",
      PositionFormationName = "Центральный (г. Минска) отдел",
      IsDepartment = true,
      LevelOrder = 1,
      Path = "/1/2/3/"
    };

    var sub_4 = new Subdivision
    {
      Id = Guid.NewGuid(),
      SubdivisionId = 4,
      ParentId = sub_2.Id,
      Name = "Фрунзенский (г. Минска) отдел",
      PositionFormationName = "Фрунзенский (г. Минска) отдел",
      IsDepartment = true,
      LevelOrder = 2,
      Path = "/1/2/4/"
    };

    var sub_5 = new Subdivision
    {
      Id = Guid.NewGuid(),
      SubdivisionId = 5,
      ParentId = sub_2.Id,
      Name = "Советский (г. Минска) отдел",
      PositionFormationName = "Советский (г. Минска) отдел",
      IsDepartment = true,
      LevelOrder = 3,
      Path = "/1/2/5/"
    };

    var sub_6 = new Subdivision
    {
      Id = Guid.NewGuid(),
      SubdivisionId = 6,
      ParentId = sub_2.Id,
      Name = "Первомайский (г. Минска) отдел",
      PositionFormationName = "Первомайский (г. Минска) отдел",
      IsDepartment = true,
      LevelOrder = 4,
      Path = "/1/2/6/"
    };

    var sub_7 = new Subdivision
    {
      Id = Guid.NewGuid(),
      SubdivisionId = 7,
      ParentId = sub_2.Id,
      Name = "Партизанский (г. Минска) отдел",
      PositionFormationName = "Партизанский (г. Минска) отдел",
      IsDepartment = true,
      LevelOrder = 5,
      Path = "/1/2/7/"
    };

    var sub_8 = new Subdivision
    {
      Id = Guid.NewGuid(),
      SubdivisionId = 8,
      ParentId = sub_2.Id,
      Name = "Отдельный батальон милиции",
      PositionFormationName = "Отдельный батальон милиции",
      IsDepartment = true,
      LevelOrder = 6,
      Path = "/1/2/8/"
    };

    var sub_9 = new Subdivision
    {
      Id = Guid.NewGuid(),
      SubdivisionId = 9,
      ParentId = sub_2.Id,
      Name = "Октябрьский (г. Минска) отдел",
      PositionFormationName = "Октябрьский (г. Минска) отдел",
      IsDepartment = true,
      LevelOrder = 7,
      Path = "/1/2/9/"
    };

    var sub_10 = new Subdivision
    {
      Id = Guid.NewGuid(),
      SubdivisionId = 10,
      ParentId = sub_2.Id,
      Name = "Московский (г. Минска) отдел",
      PositionFormationName = "Московский (г. Минска) отдел",
      IsDepartment = true,
      LevelOrder = 8,
      Path = "/1/2/10/"
    };

    var sub_11 = new Subdivision
    {
      Id = Guid.NewGuid(),
      SubdivisionId = 11,
      ParentId = sub_2.Id,
      Name = "Ленинский (г. Минска) отдел",
      PositionFormationName = "Ленинский (г. Минска) отдел",
      IsDepartment = true,
      LevelOrder = 9,
      Path = "/1/2/11/"
    };

    var sub_12 = new Subdivision
    {
      Id = Guid.NewGuid(),
      SubdivisionId = 12,
      ParentId = sub_2.Id,
      Name = "Заводской (г. Минска) отдел",
      PositionFormationName = "Заводской (г. Минска) отдел",
      IsDepartment = true,
      LevelOrder = 10,
      Path = "/1/2/12/"
    };

    await context.Subdivisions.AddRangeAsync(
      sub_1, sub_2, sub_3, sub_4, sub_5, sub_6, sub_7, sub_8, sub_9, sub_10, sub_11, sub_12
    );

    await context.SaveChangesAsync();
  }


}
