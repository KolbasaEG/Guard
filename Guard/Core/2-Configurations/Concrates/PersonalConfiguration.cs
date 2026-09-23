using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Guard.Core.Configurations;

/// <summary>
/// Конфигурация EF Core для сущности <see cref="Personal"/>.
/// </summary>
public class PersonalConfiguration : BaseEntityConfiguration<Personal>
{
  public override void Configure(EntityTypeBuilder<Personal> builder)
  {
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

    builder.Property(p => p.PersonalId)
        .ValueGeneratedOnAdd()
        .UseIdentityByDefaultColumn()
        .HasComment("Идентификатор сотрудника АИС Личное дело");

    builder.Property(x => x.PersonalSubdivisionId)
        .ValueGeneratedOnAdd()
        .UseIdentityByDefaultColumn()
        .HasComment("Уникальный автоинкрементный идентификатор подразделения сотрудника в АИС Личное дело");

    builder.HasIndex(x => x.PersonalSubdivisionId)
        .IsUnique()
        .HasDatabaseName("UX_Subdivisions_PersonalSubdivisionId");

    // ============================================================
    //                    СВЯЗИ И ВНЕШНИЕ КЛЮЧИ
    // ============================================================

    // Связь с подразделением
    builder.HasOne(p => p.Subdivision)
        .WithMany(p => p.Personals)
        .HasForeignKey(p => p.SubdivisionId)
        .OnDelete(DeleteBehavior.Restrict);

    // Классификатор категории персонала: (PersonnelCategoryType, PersonnelCategoryCode) -> (Type, Code)
    builder.HasOne(p => p.PersonnelCategory)
        .WithMany()
        .HasForeignKey(p => new { p.PersonnelCategoryType, p.PersonnelCategoryCode })
        .HasPrincipalKey(c => new { c.Type, c.Code })
        .IsRequired(false)
        .OnDelete(DeleteBehavior.Restrict);

    // Классификатор специального звания: (SpecialRankType, SpecialRankCode) -> (Type, Code)
    builder.HasOne(p => p.SpecialRank)
        .WithMany()
        .HasForeignKey(p => new { p.SpecialRankType, p.SpecialRankCode })
        .HasPrincipalKey(c => new { c.Type, c.Code })
        .IsRequired(false)
        .OnDelete(DeleteBehavior.Restrict);

    // Классификатор должности: (PositionType, PositionCode) -> (Type, Code)
    builder.HasOne(p => p.Position)
        .WithMany()
        .HasForeignKey(p => new { p.PositionType, p.PositionCode })
        .HasPrincipalKey(c => new { c.Type, c.Code })
        .IsRequired(false)
        .OnDelete(DeleteBehavior.Restrict);

    // Классификатор категории рабочего/служащего: (WorkerCategoryType, WorkerCategoryCode) -> (Type, Code)
    builder.HasOne(p => p.WorkerCategory)
        .WithMany()
        .HasForeignKey(p => new { p.WorkerCategoryType, p.WorkerCategoryCode })
        .HasPrincipalKey(c => new { c.Type, c.Code })
        .IsRequired(false)
        .OnDelete(DeleteBehavior.Restrict);

    // Классификатор статуса: (StatusType, StatusCode) -> (Type, Code)
    builder.HasOne(p => p.StatusClassifier)
        .WithMany()
        .HasForeignKey(p => new { p.StatusType, p.StatusCode })
        .HasPrincipalKey(c => new { c.Type, c.Code })
        .IsRequired(false)
        .OnDelete(DeleteBehavior.Restrict);

    // Неявная связь Многие-ко-Многим между Personal и IpAddress
    builder.HasMany(p => p.IpAddresses)
           .WithMany(i => i.Personals)
           .UsingEntity(j => j.ToTable("PersonalIpAddresses"));

    builder.HasIndex(p => p.PersonalId)
    .IsUnique()
    .HasDatabaseName("UX_Personals_PersonalId");
  }
}