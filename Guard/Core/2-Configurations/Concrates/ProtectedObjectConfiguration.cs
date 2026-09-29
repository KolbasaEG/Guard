using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Guard.Core.Configurations;

public class ProtectedObjectConfiguration : BaseEntityConfiguration<ProtectedObject>
{
  public override void Configure(EntityTypeBuilder<ProtectedObject> builder)
  {
    base.Configure(builder);

    builder.ToTable("ProtectedObjects", "public");

    builder.Property(o => o.Name)
        .IsRequired()
        .HasMaxLength(300);

    // Связь 1:N с MaintenanceSectorObject
    builder.HasMany(o => o.SectorObjects)
        .WithOne(so => so.ProtectedObject)
        .HasForeignKey(so => so.ProtectedObjectId)
        .OnDelete(DeleteBehavior.Restrict);

  }
}