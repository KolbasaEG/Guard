using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Guard.Core.Configurations;

public class MaintenanceSectorObjectConfiguration : BaseEntityConfiguration<MaintenanceSectorObject>
{
  public override void Configure(EntityTypeBuilder<MaintenanceSectorObject> builder)
  {
    base.Configure(builder);

    builder.ToTable("MaintenanceSectorObjects", "public", t =>
        t.HasComment("Связь участков обслуживания и охраняемых объектов"));

    builder.HasIndex(m => new { m.MaintenanceSectorId, m.ProtectedObjectId })
        .IsUnique()
        .HasDatabaseName("UX_MaintenanceSectorObjects_Sector_Object");

    // MaintenanceSector (1:N)
    builder.HasOne(m => m.MaintenanceSector)
        .WithMany(s => s.SectorObjects)
        .HasForeignKey(m => m.MaintenanceSectorId)
        .OnDelete(DeleteBehavior.Cascade);

    // ProtectedObject (1:N)
    builder.HasOne(m => m.ProtectedObject)
        .WithMany(o => o.SectorObjects)
        .HasForeignKey(m => m.ProtectedObjectId)
        .OnDelete(DeleteBehavior.Restrict);
  }
}