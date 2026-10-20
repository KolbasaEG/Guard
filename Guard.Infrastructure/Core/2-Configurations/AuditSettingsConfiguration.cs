using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Guard.Core.Configurations;

public sealed class AuditSettingsConfiguration : IEntityTypeConfiguration<AuditSettings>
{
  public void Configure(EntityTypeBuilder<AuditSettings> entity)
  {
    entity.ToTable("AuditSettings", t => t.HasCheckConstraint("CK_AuditSettings_Singleton", "\"Id\" = 1"));
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).ValueGeneratedNever();
    entity.Property(e => e.Host).HasMaxLength(253);
    entity.Property(e => e.CaCertificatePem).HasMaxLength(16384);
    entity.Property(e => e.ModifiedBy).HasMaxLength(256);
    entity.Property(e => e.Version).IsConcurrencyToken();
  }
}

public sealed class AuditDeliveryAttemptConfiguration : IEntityTypeConfiguration<AuditDeliveryAttempt>
{
  public void Configure(EntityTypeBuilder<AuditDeliveryAttempt> entity)
  {
    entity.ToTable("AuditDeliveryAttempts");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).ValueGeneratedNever();
    entity.Property(e => e.Destination).HasMaxLength(280);
    entity.Property(e => e.Error).HasMaxLength(256);
    entity.HasIndex(e => new { e.EventId, e.StartedAtUtc });
    entity.HasOne<AuditEventRecord>().WithMany().HasForeignKey(e => e.EventId).OnDelete(DeleteBehavior.Cascade);
  }
}
