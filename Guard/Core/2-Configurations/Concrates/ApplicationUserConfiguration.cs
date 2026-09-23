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
    // Настройка связи 1-к-1 с Personal
    builder.HasOne(u => u.Personal)
           .WithOne(p => p.User)
           .HasForeignKey<ApplicationUser>(u => u.PersonalId)
           .OnDelete(DeleteBehavior.SetNull);

    builder.HasIndex(u => u.PersonalId)
           .IsUnique();
  }
}