using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Guard.Core.Configurations;

public class MaintenanceSectorConfiguration : BaseEntityConfiguration<MaintenanceSector>
{
  public override void Configure(EntityTypeBuilder<MaintenanceSector> builder)
  {
    base.Configure(builder);

    builder.ToTable("MaintenanceSectors", "public", t =>
        t.HasComment("Участки обслуживания охраняемых объектов"));

    builder.Property(s => s.Name)
        .IsRequired()
        .HasMaxLength(256)
        .HasComment("Наименование участка обслуживания");

    builder.Property(s => s.Description)
        .HasMaxLength(1000)
        .HasComment("Описание участка");

    builder.Property(s => s.CreatedAt)
        .IsRequired()
        .HasComment("Дата создания участка");

    // Subdivision (1:N)
    builder.HasOne(s => s.Subdivision)
        .WithMany(sub => sub.MaintenanceSectors)
        .HasForeignKey(s => s.SubdivisionId)
        .OnDelete(DeleteBehavior.Restrict);

    // Responsible Personal (1:N)
    builder.HasOne(s => s.ResponsiblePersonal)
        .WithMany(p => p.ResponsibleSectors)
        .HasForeignKey(s => s.ResponsiblePersonalId)
        .OnDelete(DeleteBehavior.Restrict);

    builder.HasIndex(s => s.SubdivisionId);
    builder.HasIndex(s => s.ResponsiblePersonalId);
  }
}