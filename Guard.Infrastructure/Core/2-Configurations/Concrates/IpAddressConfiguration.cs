using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Guard.Core.Contexts;
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

    builder.Property(x => x.Description)
        .HasMaxLength(1000);

    // Связь с подразделением
    builder.HasOne(p => p.Subdivision)
        .WithMany(p => p.IpAddresses)
        .HasForeignKey(p => p.SubdivisionId)
        .OnDelete(DeleteBehavior.Restrict);

    builder.HasIndex(x => x.Address).IsUnique();
    // Non-unique: preserve equivalent legacy rows and their assignments during backfill.
    builder.Property<string>(IpCatalogKeys.Canonical).HasMaxLength(50);
    builder.Property<string>(IpCatalogKeys.Source).HasMaxLength(50);
    builder.HasIndex(IpCatalogKeys.Canonical);
    builder.HasIndex(x => x.Id).HasDatabaseName("IX_IpAddresses_MissingCanonicalAddress")
      .HasFilter("\"CanonicalAddress\" IS NULL");
    builder.ToTable(t => t.HasCheckConstraint(IpCatalogKeys.SourceConstraint, IpCatalogKeys.SourceCheck));
  }
}
