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
  }

  private static async Task EnsureRoleAsync(RoleManager<ApplicationRole> roleManager, string roleName)
  {
    if (!await roleManager.RoleExistsAsync(roleName))
    {
      await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
    }
  }
}
