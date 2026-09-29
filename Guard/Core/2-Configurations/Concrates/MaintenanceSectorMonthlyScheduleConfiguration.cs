using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Guard.Core.Configurations;

public class MaintenanceSectorMonthlyScheduleConfiguration : BaseEntityConfiguration<MaintenanceSectorMonthlySchedule>
{
  public override void Configure(EntityTypeBuilder<MaintenanceSectorMonthlySchedule> builder)
  {
    base.Configure(builder);

    builder.ToTable("MaintenanceSectorMonthlySchedules", "public", t =>
        t.HasComment("Календарный график работы участка на месяц"));

    // Уникальный индекс: одна дата на один участок
    builder.HasIndex(x => new { x.MaintenanceSectorId, x.Date })
        .IsUnique()
        .HasDatabaseName("UX_MaintenanceSectorMonthlySchedules_Sector_Date");

    builder.Property(x => x.Date)
        .IsRequired()
        .HasComment("Календарная дата");

    builder.Property(x => x.IsWorkDay)
        .IsRequired()
        .HasComment("Рабочая смена участка");

    builder.Property(x => x.WorkStart)
        .HasComment("Время начала смены участка");

    builder.Property(x => x.WorkEnd)
        .HasComment("Время окончания смены участка");

    builder.Property(x => x.Note)
        .HasMaxLength(500)
        .HasComment("Примечание к смене (праздник, сокращенный день)");

    // MaintenanceSector (1:N)
    builder.HasOne(x => x.MaintenanceSector)
        .WithMany(s => s.MonthlySchedules)
        .HasForeignKey(x => x.MaintenanceSectorId)
        .OnDelete(DeleteBehavior.Cascade);
  }
}