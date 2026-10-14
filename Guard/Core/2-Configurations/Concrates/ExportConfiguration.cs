using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Guard.Core.Configurations;

public sealed class ExportJobConfiguration : IEntityTypeConfiguration<ExportJob>
{
  public void Configure(EntityTypeBuilder<ExportJob> b)
  {
    b.ToTable("ExportJobs"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
    b.Property(x => x.AuthorId).HasMaxLength(450); b.Property(x => x.AuthorName).HasMaxLength(256);
    b.Property(x => x.ClientIp).HasMaxLength(64); b.Property(x => x.SecurityStamp).HasMaxLength(256);
    b.Property(x => x.RequestJson).HasColumnType("jsonb"); b.Property(x => x.Error).HasMaxLength(500);
    b.HasIndex(x => new { x.State, x.CreatedAtUtc }); b.HasIndex(x => x.ExpiresAtUtc);
    b.HasIndex(x => new { x.AuthorId, x.CreatedAtUtc });
  }
}
public sealed class ExportArtifactConfiguration : IEntityTypeConfiguration<ExportArtifact>
{
  public void Configure(EntityTypeBuilder<ExportArtifact> b)
  {
    b.ToTable("ExportArtifacts"); b.HasKey(x => x.JobId);
    b.HasOne(x => x.Job).WithOne(x => x.Artifact).HasForeignKey<ExportArtifact>(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
    b.Property(x => x.FileName).HasMaxLength(256); b.Property(x => x.StorageKey).HasMaxLength(100);
    b.Property(x => x.ContentType).HasMaxLength(150); b.Property(x => x.Sha256).HasMaxLength(64);
    b.Property(x => x.SubdivisionIdsJson).HasColumnType("jsonb"); b.HasIndex(x => x.StorageKey).IsUnique();
  }
}
public sealed class ExportDeliveryConfiguration : IEntityTypeConfiguration<ExportDeliveryAttempt>
{
  public void Configure(EntityTypeBuilder<ExportDeliveryAttempt> b)
  {
    b.ToTable("ExportDeliveryAttempts"); b.HasKey(x => x.JobId);
    b.HasOne(x => x.Job).WithOne(x => x.Delivery).HasForeignKey<ExportDeliveryAttempt>(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
  }
}
public sealed class ExportRetentionConfiguration : IEntityTypeConfiguration<ExportRetentionSettings>
{
  public void Configure(EntityTypeBuilder<ExportRetentionSettings> b)
  {
    b.ToTable("ExportRetentionSettings", t => {
      t.HasCheckConstraint("CK_ExportRetentionSettings_Id", "\"Id\" = 1");
      t.HasCheckConstraint("CK_ExportRetentionSettings_Years", "\"Years\" BETWEEN 1 AND 30");
    });
    b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever(); b.Property(x => x.Version).IsConcurrencyToken();
    b.Property(x => x.ModifiedBy).HasMaxLength(450);
  }
}
