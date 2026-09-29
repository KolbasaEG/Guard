using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Guard.Core.Configurations;

public class MaintenanceSectorWeeklyPatternConfiguration : BaseEntityConfiguration<MaintenanceSectorWeeklyPattern>
{
  public override void Configure(EntityTypeBuilder<MaintenanceSectorWeeklyPattern> builder)
  {
    base.Configure(builder);

    builder.ToTable("MaintenanceSectorWeeklyPatterns", "public", t =>
        t.HasComment("Шаблон рабочей недели участка обслуживания (7 дней)"));

    // Уникальный индекс: у одного участка может быть только один шаблон на конкретный день недели
    builder.HasIndex(x => new { x.MaintenanceSectorId, x.DayOfWeek })
        .IsUnique()
        .HasDatabaseName("UX_MaintenanceSectorWeeklyPatterns_Sector_DayOfWeek");

    builder.Property(x => x.DayOfWeek)
        .IsRequired()
        .HasComment("День недели (0 = Sunday, 1 = Monday...)");

    builder.Property(x => x.IsWorkDay)
        .IsRequired()
        .HasComment("Признак рабочего дня");

    builder.Property(x => x.WorkStart)
        .HasComment("Время начала смены");

    builder.Property(x => x.WorkEnd)
        .HasComment("Время окончания смены");

    // MaintenanceSector (1:N)
    builder.HasOne(x => x.MaintenanceSector)
        .WithMany(s => s.WeeklyPatterns)
        .HasForeignKey(x => x.MaintenanceSectorId)
        .OnDelete(DeleteBehavior.Cascade);
  }
}