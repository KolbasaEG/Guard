using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Guard.Core.Configurations;

public class SynchronizationSettingsConfiguration : IEntityTypeConfiguration<SynchronizationSettings>
{
  public void Configure(EntityTypeBuilder<SynchronizationSettings> builder)
  {
    builder.HasKey(s => s.Id);
    builder.Property(s => s.Id).ValueGeneratedNever();
    builder.Property(s => s.Version).IsConcurrencyToken();
    builder.Property(s => s.ModifiedBy).HasMaxLength(450);
    builder.Property(s => s.ApiBaseUrl).HasMaxLength(2048);
    builder.Property(s => s.ApiUsername).HasMaxLength(256);
    builder.Property(s => s.ApiHeadSubdivisionId).HasColumnType("bigint");
    builder.ToTable("SynchronizationSettings", table => table.HasCheckConstraint(
      "CK_SynchronizationSettings_ApiHeadSubdivisionId", "\"ApiHeadSubdivisionId\" IS NULL OR \"ApiHeadSubdivisionId\" > 0"));
    builder.HasData(new SynchronizationSettings { Version = new Guid("ed7ff5f9-f6da-4f37-807b-28b919ec26f2") });
  }
}

public class SynchronizationRunConfiguration : IEntityTypeConfiguration<SynchronizationRun>
{
  public void Configure(EntityTypeBuilder<SynchronizationRun> builder)
  {
    builder.HasKey(r => r.Id);
    builder.Property(r => r.ActorId).HasMaxLength(450);
    builder.Property(r => r.Error).HasMaxLength(1000);
    builder.Property(r => r.Phase).HasMaxLength(40);
    builder.HasIndex(r => new { r.PoolId, r.StartedAt });
    builder.HasIndex(r => r.Status);
  }
}

public class ApiPersonnelExitConfiguration : IEntityTypeConfiguration<ApiPersonnelExit>
{
  public void Configure(EntityTypeBuilder<ApiPersonnelExit> builder)
  {
    builder.HasKey(e => e.PersonalExternalId);
    builder.Property(e => e.PersonalExternalId).ValueGeneratedNever();
    builder.ToTable("ApiPersonnelExits");
  }
}
