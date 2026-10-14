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


    if (!await context.SynchronizationSettings.AnyAsync(s => s.Id == 1 && s.Mode == DataManagementMode.Api))
      await SeedSubdivisionsAsync(context);
  }

  private static async Task EnsureRoleAsync(RoleManager<ApplicationRole> roleManager, string roleName)
  {
    if (!await roleManager.RoleExistsAsync(roleName))
    {
      await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
    }
  }
  private static async Task SeedSubdivisionsAsync(ApplicationDbContext context)
  {
    if (await context.Subdivisions.AnyAsync())
    {
      return;
    }

    var hasStatus = await context.Classifiers.AnyAsync(c => c.Type == 362 && c.Code == 1);
    // Идентификаторы для организации Guid-связей иерархии в EF Core
    var rootId = Guid.NewGuid(); // Тестовое главное управление (1001)
    var uitId = Guid.NewGuid();  // Управление информационных технологий (1004)

    var subdivisions = new List<Subdivision>
    {
      new Subdivision
      {
        Id = rootId,
        SubdivisionId = 1001,
        ParentSubdivisionId = null,
        ParentId = null,
        Name = "Тестовое главное управление",
        PositionFormationName = "ГУ",
        PostalCode = "220000",
        Address = "г. Минск, ул. Тестовая, д. 1",
        Phone = "+375 (17) 000-10-01",
        Fax = "+375 (17) 000-20-01",
        IsDepartment = false,
        StaffCount = 100.5,
        LevelOrder = 1,
        Path = "/1001/",
        StatusType = hasStatus ? 362 : null,
        StatusCode = hasStatus ? 1 : null,
        OrganTypeId = null,
        OrganTypeCode = null,
        UpdatedAt = DateTime.SpecifyKind(DateTime.Parse("2026-08-08T09:00:00Z"), DateTimeKind.Utc)
      },
      new Subdivision
      {
        Id = Guid.NewGuid(),
        SubdivisionId = 1002,
        ParentSubdivisionId = 1001,
        ParentId = rootId,
        Name = "Отдел кадров",
        PositionFormationName = "ОК",
        PostalCode = "220000",
        Address = "г. Минск, ул. Тестовая, д. 1, каб. 201",
        Phone = "+375 (17) 000-10-02",
        Fax = "+375 (17) 000-20-02",
        IsDepartment = true,
        StaffCount = 18.0,
        LevelOrder = 1,
        Path = "/1001/1002/",
        StatusType = hasStatus ? 362 : null,
        StatusCode = hasStatus ? 1 : null,
        OrganTypeId = null,
        OrganTypeCode = null,
        UpdatedAt = DateTime.SpecifyKind(DateTime.Parse("2026-08-09T09:00:00Z"), DateTimeKind.Utc)
      },
      new Subdivision
      {
        Id = Guid.NewGuid(),
        SubdivisionId = 1003,
        ParentSubdivisionId = 1001,
        ParentId = rootId,
        Name = "Финансовый отдел",
        PositionFormationName = "ФО",
        PostalCode = "220000",
        Address = "г. Минск, ул. Тестовая, д. 1, каб. 301",
        Phone = "+375 (17) 000-10-03",
        Fax = "+375 (17) 000-20-03",
        IsDepartment = true,
        StaffCount = 12.5,
        LevelOrder = 2,
        Path = "/1001/1003/",
        StatusType = hasStatus ? 362 : null,
        StatusCode = hasStatus ? 1 : null,
        OrganTypeId = null,
        OrganTypeCode = null,
        UpdatedAt = DateTime.SpecifyKind(DateTime.Parse("2026-08-10T08:00:00Z"), DateTimeKind.Utc)
      },
      new Subdivision
      {
        Id = uitId,
        SubdivisionId = 1004,
        ParentSubdivisionId = 1001,
        ParentId = rootId,
        Name = "Управление информационных технологий",
        PositionFormationName = "УИТ",
        PostalCode = "220000",
        Address = "г. Минск, ул. Тестовая, д. 2",
        Phone = "+375 (17) 000-10-04",
        Fax = "+375 (17) 000-20-04",
        IsDepartment = false,
        StaffCount = 30.0,
        LevelOrder = 3,
        Path = "/1001/1004/",
        StatusType = hasStatus ? 362 : null,
        StatusCode = hasStatus ? 1 : null,
        OrganTypeId = null,
        OrganTypeCode = null,
        UpdatedAt = DateTime.SpecifyKind(DateTime.Parse("2026-08-11T08:00:00Z"), DateTimeKind.Utc)
      },
      new Subdivision
      {
        Id = Guid.NewGuid(),
        SubdivisionId = 1005,
        ParentSubdivisionId = 1004,
        ParentId = uitId,
        Name = "Отдел сопровождения систем",
        PositionFormationName = "ОСС",
        PostalCode = "220000",
        Address = "г. Минск, ул. Тестовая, д. 2, каб. 101",
        Phone = "+375 (17) 000-10-05",
        Fax = "+375 (17) 000-20-05",
        IsDepartment = true,
        StaffCount = 15.5,
        LevelOrder = 1,
        Path = "/1001/1004/1005/",
        StatusType = hasStatus ? 362 : null,
        StatusCode = hasStatus ? 1 : null,
        OrganTypeId = null,
        OrganTypeCode = null,
        UpdatedAt = DateTime.SpecifyKind(DateTime.Parse("2026-08-12T08:00:00Z"), DateTimeKind.Utc)
      }
    };

    await context.Subdivisions.AddRangeAsync(subdivisions);
    await context.SaveChangesAsync();
  }


}
