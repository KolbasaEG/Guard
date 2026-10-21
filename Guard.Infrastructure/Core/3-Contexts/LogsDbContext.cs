using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Contexts;

public class LogsDbContext : DbContext
{
  public LogsDbContext(DbContextOptions<LogsDbContext> options) : base(options) { }

  public DbSet<LogEntry> Logs => Set<LogEntry>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<LogEntry>(entity =>
    {
      entity.ToTable("logs");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
      entity.Property(e => e.Properties).HasColumnName("properties").HasColumnType("jsonb");
      entity.Property(e => e.MessageTemplate).HasColumnName("message_template");
      entity.Property(e => e.Timestamp).HasColumnName("timestamp")
        .HasColumnType("timestamp without time zone")
        .HasConversion(value => DateTime.SpecifyKind(value.Kind == DateTimeKind.Unspecified ? value : value.ToUniversalTime(), DateTimeKind.Unspecified),
          value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
      entity.Property(e => e.Level).HasColumnName("level");
      entity.Property(e => e.Layer).HasColumnName("layer");
      entity.Property(e => e.Message).HasColumnName("message");
      entity.Property(e => e.Exception).HasColumnName("exception");
      entity.Property(e => e.UserId).HasColumnName("user_id");
      entity.Property(e => e.ClientIp).HasColumnName("ip_address");
      entity.Property(e => e.CorrelationId).HasColumnName("correlation_id");
      entity.Property(e => e.SourceContext).HasColumnName("source_context");
      entity.Property(e => e.DiagnosticSessionId).HasColumnName("diagnostic_session_id");
      entity.Property(e => e.RunId).HasColumnName("run_id");
      entity.Property(e => e.Fingerprint).HasColumnName("fingerprint");
      entity.Property(e => e.ApplicationVersion).HasColumnName("application_version");
      entity.Property(e => e.InstanceId).HasColumnName("instance_id");
    });
  }
}
