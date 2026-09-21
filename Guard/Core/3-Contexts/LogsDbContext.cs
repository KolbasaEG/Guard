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
      entity.HasNoKey(); 
      entity.Property(e => e.Timestamp).HasColumnName("timestamp");
      entity.Property(e => e.Level).HasColumnName("level");
      entity.Property(e => e.Layer).HasColumnName("layer");
      entity.Property(e => e.Message).HasColumnName("message");
      entity.Property(e => e.Exception).HasColumnName("exception");
      entity.Property(e => e.UserId).HasColumnName("user_id");
      entity.Property(e => e.ClientIp).HasColumnName("ip_address");
    });
  }
}