using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Guard.Core.Configurations;

public sealed class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
  public void Configure(EntityTypeBuilder<UserNotification> b)
  {
    b.ToTable("UserNotifications"); b.HasKey(x => x.Id);
    b.Property(x => x.UserId).HasMaxLength(450);
    b.Property(x => x.EventKey).HasMaxLength(200);
    b.Property(x => x.Title).HasMaxLength(200); b.Property(x => x.Message).HasMaxLength(2000);
    b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    b.HasOne<NotificationIncident>().WithMany().HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.Restrict);
    b.HasIndex(x => new { x.UserId, x.EventKey }).IsUnique();
    b.HasIndex(x => new { x.UserId, x.UpdatedAtUtc, x.Id });
    b.HasIndex(x => new { x.UserId, x.Revision });
    b.HasIndex(x => new { x.UserId, x.Revision }, "IX_UserNotifications_Unread")
      .HasFilter("\"ReadRevision\" < \"Revision\"");
    b.HasIndex(x => x.ReadAtUtc).HasFilter("NOT \"IncidentActive\" AND \"ReadRevision\" = \"Revision\"");
  }
}
public sealed class NotificationIncidentConfiguration : IEntityTypeConfiguration<NotificationIncident>
{
  public void Configure(EntityTypeBuilder<NotificationIncident> b)
  {
    b.ToTable("NotificationIncidents"); b.HasKey(x => x.Id); b.Property(x => x.SourceKey).HasMaxLength(200);
    b.HasIndex(x => x.SourceKey).IsUnique().HasFilter("\"Active\"");
  }
}
public sealed class NotificationEventConfiguration : IEntityTypeConfiguration<NotificationEvent>
{
  public void Configure(EntityTypeBuilder<NotificationEvent> b)
  {
    b.ToTable("NotificationEvents"); b.HasKey(x => x.Id);
    b.Property(x => x.EventKey).HasMaxLength(200); b.HasIndex(x => x.EventKey).IsUnique();
    b.Property(x => x.UserId).HasMaxLength(450); b.Property(x => x.SourceKey).HasMaxLength(200);
    b.Property(x => x.Title).HasMaxLength(200); b.Property(x => x.Message).HasMaxLength(2000);
    b.HasIndex(x => new { x.OccurredAtUtc, x.Id }).HasFilter("\"ProcessedAtUtc\" IS NULL");
  }
}
