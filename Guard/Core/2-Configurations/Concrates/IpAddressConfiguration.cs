using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Guard.Core.Configurations;

/// <summary>
/// EF Core конфигурация сущности IpAddress.
/// </summary>
public class IpAddressConfiguration : IEntityTypeConfiguration<IpAddress>
{
  public void Configure(EntityTypeBuilder<IpAddress> builder)
  {
    // =====================================================
    // НАЗВАНИЕ ТАБЛИЦЫ: IpAddresses
    // ОПИСАНИЕ: Справочник IP-адресов и CIDR-подсетей системы Guard.
    //           Центральный каталог всех известных IP-адресов и подсетей,
    //           которые могут быть назначены пользователям, подразделениям и другим сущностям.
    // =====================================================
    builder.ToTable("IpAddresses", t => t.HasComment(@"Справочник IP-адресов и CIDR-подсетей системы Guard. Центральный каталог всех известных IP-адресов и подсетей, которые могут быть назначены пользователям, подразделениям и другим сущностям."));

    builder.HasKey(x => x.Id);

    builder.Property(x => x.Address)
        .IsRequired()
        .HasMaxLength(50)
        .HasComment("IP-адрес или CIDR-нотация");

    builder.Property(x => x.Name)
        .IsRequired()
        .HasMaxLength(200)
        .HasComment("Человекочитаемое название");

    builder.Property(x => x.Description)
        .HasMaxLength(1000);

    builder.HasIndex(x => x.Address).IsUnique();
  }
}
