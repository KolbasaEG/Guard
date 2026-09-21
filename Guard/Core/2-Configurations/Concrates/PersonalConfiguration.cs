using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Guard.Core.Configurations;

/// <summary>
/// Конфигурация EF Core для сущности <see cref="Personnel"/>.
/// </summary>
public class PersonnelConfiguration : BaseEntityConfiguration<Personal>
{
  public override void Configure(EntityTypeBuilder<Personal> builder)
  {
    // Вызов базовой конфигурации (Primary Key Guid UUIDv7, Status enum, Audit fields)
    base.Configure(builder);

    builder.ToTable("Personals", t =>
    {
      t.HasComment("Справочник персонала системы Guard.");
    });

    // ============================================================
    //                    ПЕРСОНАЛЬНЫЕ ДАННЫЕ
    // ============================================================
    builder.Property(p => p.LastName)
        .IsRequired()
        .HasMaxLength(100)
        .HasComment("Фамилия");

    builder.Property(p => p.FirstName)
        .IsRequired()
        .HasMaxLength(100)
        .HasComment("Имя");

    builder.Property(p => p.MiddleName)
        .HasMaxLength(100)
        .HasComment("Отчество");

    builder.Property(p => p.FullName)
        .HasMaxLength(250)
        .HasComment("Фамилия и инициалы");

    builder.Property(p => p.LastNameGen)
        .HasMaxLength(100)
        .HasComment("Фамилия в родительном падеже");

    builder.Property(p => p.FirstNameGen)
        .HasMaxLength(100)
        .HasComment("Имя в родительном падеже");

    builder.Property(p => p.MiddleNameGen)
        .HasMaxLength(100)
        .HasComment("Отчество в родительном падеже");

    builder.Property(p => p.PersonalNumber)
        .HasMaxLength(50)
        .HasComment("Личный номер");

    builder.Property(p => p.EnlistmentYear)
        .HasComment("Год принятия на службу");

    builder.Property(p => p.UpdatedAt)
        .HasComment("Дата последнего обновления информации");

    // ============================================================
    //                    СВЯЗИ И ВНЕШНИЕ КЛЮЧИ
    // ============================================================

    // Связь с подразделением
    builder.HasOne(p => p.Subdivision)
        .WithMany()
        .HasForeignKey(p => p.SubdivisionId)
        .OnDelete(DeleteBehavior.Restrict);

    // Классификаторы категории персонала
    builder.HasOne(p => p.PersonnelCategoryType)
        .WithMany()
        .HasForeignKey(p => p.PersonnelCategoryTypeId)
        .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne(p => p.PersonnelCategoryCode)
        .WithMany()
        .HasForeignKey(p => p.PersonnelCategoryCodeId)
        .OnDelete(DeleteBehavior.Restrict);

    // Классификаторы специального звания
    builder.HasOne(p => p.SpecialRankType)
        .WithMany()
        .HasForeignKey(p => p.SpecialRankTypeId)
        .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne(p => p.SpecialRankCode)
        .WithMany()
        .HasForeignKey(p => p.SpecialRankCodeId)
        .OnDelete(DeleteBehavior.Restrict);

    // Классификаторы должности
    builder.HasOne(p => p.PositionType)
        .WithMany()
        .HasForeignKey(p => p.PositionTypeId)
        .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne(p => p.PositionCode)
        .WithMany()
        .HasForeignKey(p => p.PositionCodeId)
        .OnDelete(DeleteBehavior.Restrict);

    // Классификаторы категории рабочего/служащего
    builder.HasOne(p => p.WorkerCategoryType)
        .WithMany()
        .HasForeignKey(p => p.WorkerCategoryTypeId)
        .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne(p => p.WorkerCategoryCode)
        .WithMany()
        .HasForeignKey(p => p.WorkerCategoryCodeId)
        .OnDelete(DeleteBehavior.Restrict);

    // Классификаторы статуса
    builder.HasOne(p => p.StatusType)
        .WithMany()
        .HasForeignKey(p => p.StatusTypeId)
        .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne(p => p.StatusCode)
        .WithMany()
        .HasForeignKey(p => p.StatusCodeId)
        .OnDelete(DeleteBehavior.Restrict);
  }
}