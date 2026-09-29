using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Guard.Core.Configurations;

public class MaintenanceSectorObjectScheduleConfiguration : BaseEntityConfiguration<MaintenanceSectorObjectSchedule>
{
  public override void Configure(EntityTypeBuilder<MaintenanceSectorObjectSchedule> builder)
  {
    base.Configure(builder);

    builder.ToTable("MaintenanceSectorObjectSchedules", "public", t =>
        t.HasComment("График обслуживания объектов участка (крестики по датам)"));

    // Уникальный индекс: один объект не может иметь дублирующие записи на одну и ту же дату в рамках участка
    builder.HasIndex(x => new { x.MaintenanceSectorId, x.ProtectedObjectId, x.Date })
        .IsUnique()
        .HasDatabaseName("UX_MaintenanceSectorObjectSchedules_Sector_Object_Date");

    builder.Property(x => x.Date)
        .IsRequired()
        .HasComment("Дата запланированного регламента");

    builder.Property(x => x.IsScheduled)
        .IsRequired()
        .HasComment("Отметка планирования (крестик)");

    builder.Property(x => x.IsCompleted)
        .IsRequired()
        .HasComment("Отметка о фактическом выполнении");

    builder.Property(x => x.Note)
        .HasMaxLength(500)
        .HasComment("Вид регламента или примечание");

    // MaintenanceSector (1:N)
    builder.HasOne(x => x.MaintenanceSector)
        .WithMany(s => s.ObjectSchedules)
        .HasForeignKey(x => x.MaintenanceSectorId)
        .OnDelete(DeleteBehavior.Cascade);

    // ProtectedObject (1:N)
    builder.HasOne(x => x.ProtectedObject)
        .WithMany()
        .HasForeignKey(x => x.ProtectedObjectId)
        .OnDelete(DeleteBehavior.Restrict);
  }
}