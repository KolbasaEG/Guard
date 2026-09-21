using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Guard.Core.Configurations;

/// <summary>
/// Конфигурация ApplicationUser.
/// Настраивает many-to-many связь с IpAddress через явную join-сущность UserIpAddress.
/// </summary>
public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
  public void Configure(EntityTypeBuilder<ApplicationUser> builder)
  {
    // Простая и надёжная настройка many-to-many
    // EF Core автоматически использует навигационные свойства из UserIpAddress
    builder.HasMany(u => u.IpAddresses)
           .WithMany(i => i.Users)
           .UsingEntity<UserIpAddress>();
  }
}