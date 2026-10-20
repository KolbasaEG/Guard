using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Guard.Core.Configurations;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEventRecord>
{
  public void Configure(EntityTypeBuilder<AuditEventRecord> entity)
  {
    entity.ToTable("AuditEvents");
    entity.HasKey(e => e.EventId);
    entity.Property(e => e.EventId).ValueGeneratedNever();
    entity.Property(e => e.Actor).HasMaxLength(256);
    entity.Property(e => e.ActorKind).HasMaxLength(16);
    entity.Property(e => e.Outcome).HasMaxLength(16);
    entity.Property(e => e.ClientIp).HasMaxLength(64);
    entity.Property(e => e.CorrelationId).HasMaxLength(128);
    entity.Property(e => e.ObjectType).HasMaxLength(64);
    entity.Property(e => e.ObjectId).HasMaxLength(128);
    entity.Property(e => e.Details).HasMaxLength(16384);
    entity.Property(e => e.LastError).HasMaxLength(128);
    entity.Property(e => e.ReasonCode).HasMaxLength(128);
    entity.Property(e => e.AttemptedAccount).HasMaxLength(256);
    entity.HasIndex(e => new { e.OccurredAtUtc, e.EventId });
    entity.HasIndex(e => new { e.NextAttemptAtUtc, e.EventId }).HasFilter("\"SentAtUtc\" IS NULL");
    entity.HasIndex(e => new { e.QueuedAtUtc, e.EventId }).HasFilter("\"DeliveryRequired\" AND \"SentAtUtc\" IS NULL");
  }
}
