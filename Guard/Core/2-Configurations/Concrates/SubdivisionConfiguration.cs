using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Guard.Core.Configurations;

public class SubdivisionConfiguration : BaseEntityConfiguration<Subdivision>
{
  public override void Configure(EntityTypeBuilder<Subdivision> builder)
  {
    base.Configure(builder);

    builder.ToTable("Subdivisions", t => t.HasComment("Иерархический справочник подразделений организационной структуры системы Guard."));

    builder.Property(x => x.SubdivisionId)
        .IsRequired()
        .ValueGeneratedOnAdd()
        .UseIdentityByDefaultColumn()
        .HasComment("Уникальный автоинкрементный идентификатор для Path");

    builder.Property(x => x.ParentSubdivisionId)
        .HasComment("идентификатор родителя");

    builder.HasIndex(x => x.ParentSubdivisionId)
        .HasDatabaseName("UX_Subdivisions_ParentSubdivisionId");

    builder.HasIndex(x => x.SubdivisionId)
        .IsUnique()
        .HasDatabaseName("UX_Subdivisions_SubdivisionId");

    builder.Property(x => x.Name)
        .IsRequired()
        .HasMaxLength(500)
        .HasComment("Полное наименование подразделения");

    builder.Property(x => x.PositionFormationName)
        .HasMaxLength(500)
        .HasComment("Наименование для формирования должности");

    builder.Property(x => x.PostalCode)
        .HasMaxLength(20)
        .HasComment("Почтовый индекс");

    builder.Property(x => x.Address)
        .HasMaxLength(1000)
        .HasComment("Адрес");

    builder.Property(x => x.Phone)
        .HasMaxLength(50)
        .HasComment("Телефон");

    builder.Property(x => x.Fax)
        .HasMaxLength(50)
        .HasComment("Факс");

    builder.Property(x => x.StaffCount)
        .HasComment("Снимок штатной численности (может устаревать)");

    builder.Property(x => x.LevelOrder)
        .HasComment("Порядковый номер внутри одного уровня иерархии");

    builder.Property(x => x.Path)
        .HasComment("Путь подразделения");

    builder.Property(x => x.IsDepartment)
    .HasDefaultValue(false)
    .HasComment("Признак: является ли подразделение отделом");

    builder.Property(p => p.UpdatedAt)
        .HasComment("Дата последнего обновления информации");

    // Иерархия (Parent -> Children)
    builder.HasOne(x => x.Parent)
        .WithMany(x => x.Children)
        .HasForeignKey(x => x.ParentId)
        .OnDelete(DeleteBehavior.Restrict);

    // Составной внешний ключ на Classifiers: (StatusType, StatusCode) -> (Type, Code)
    builder.HasOne(s => s.StatusClassifier)
        .WithMany(p => p.Subdivisions)
        .HasForeignKey(s => new { s.StatusType, s.StatusCode })
        .HasPrincipalKey(c => new { c.Type, c.Code })
        .IsRequired(false)
        .OnDelete(DeleteBehavior.Restrict);

    // Составной внешний ключ на OrganTypes: (OrganTypeId, OrganTypeCode) -> (Id, Code)
    builder.HasOne(s => s.OrganType)
        .WithMany(p => p.Subdivisions)
        .HasForeignKey(s => new { s.OrganTypeId, s.OrganTypeCode })
        .HasPrincipalKey(o => new { o.Id, o.Code })
        .IsRequired(false)
        .OnDelete(DeleteBehavior.Restrict);

    // Индексы
    builder.HasIndex(x => x.ParentId);

    builder.HasIndex(s => s.Path)
        .HasOperators("varchar_pattern_ops");

    builder.HasIndex(x => new { x.Status, x.InsertedDate })
        .IsDescending(false, true)
        .HasDatabaseName("IX_Subdivisions_Status_InsertedDate");
  }
}