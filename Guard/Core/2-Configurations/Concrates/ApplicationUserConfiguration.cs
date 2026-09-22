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

    // Настройка связи 1-к-1 с Personal
    builder.HasOne(u => u.Personal)
           .WithOne(p => p.User)
           .HasForeignKey<ApplicationUser>(u => u.PersonalId)
           .OnDelete(DeleteBehavior.SetNull); // При удалении сотрудника отвязываем пользователя (PersonalId = null)

    // Уникальный индекс, гарантирующий связь именно 1 к 1
    builder.HasIndex(u => u.PersonalId)
           .IsUnique()
           .HasFilter("\"PersonalId\" IS NOT NULL");
  }
}