using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Guard.Core.Configurations;

/// <summary>
/// Конфигурация ApplicationUser.
/// Пользователь связан с одним сотрудником; IP назначаются сотруднику через PersonalIpAddresses.
/// </summary>
public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
  public void Configure(EntityTypeBuilder<ApplicationUser> builder)
  {
    builder.Property(u => u.AccountBlockReason).HasMaxLength(64);
    builder.HasIndex(u => u.LastActivityAtUtc);
    // Настройка связи 1-к-1 с Personal
    builder.HasOne(u => u.Personal)
           .WithOne(p => p.User)
           .HasForeignKey<ApplicationUser>(u => u.PersonalId)
           .OnDelete(DeleteBehavior.SetNull);

    builder.HasIndex(u => u.PersonalId)
           .IsUnique();
  }
}
