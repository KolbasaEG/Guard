using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Guard.Core.Configurations;

public class SubdivisionConfiguration : BaseEntityConfiguration<Subdivision>
{
  public override void Configure(EntityTypeBuilder<Subdivision> builder)
  {
    builder.ToTable("Subdivisions", t => t.HasComment(@"Иерархический справочник подразделений / отделов / органов организационной структуры системы Guard."));

    builder.Property(x => x.SubdivisionId)
        .ValueGeneratedOnAdd()
        .UseIdentityByDefaultColumn() 
        .HasComment("Уникальный автоинкрементный идентификатор для Path");

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

    builder.HasOne(x => x.Parent)
           .WithMany(x => x.Childrens)
           .HasForeignKey(x => x.ParentId)
           .OnDelete(DeleteBehavior.Restrict); 

    builder.HasOne(s => s.StatusType)
        .WithMany()
        .HasForeignKey(s => s.StatusTypeId)
        .IsRequired(false)
        .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne(s => s.StatusCode)
        .WithMany()
        .HasForeignKey(s => s.StatusCodeId)
        .IsRequired(false)
        .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne(s => s.OrganTypeCode)
        .WithMany()
        .HasForeignKey(s => s.OrganTypeCodeId)
        .IsRequired(false)
        .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne(s => s.OrganType)
        .WithMany()
        .HasForeignKey(s => s.OrganTypeId)
        .IsRequired(false)
        .OnDelete(DeleteBehavior.Restrict);

    // Индексы
    // 1. Поиск по родителям 
    builder.HasIndex(x => x.ParentId);

    // 2. Поиск по путям 
    builder.HasIndex(s => s.Path)
        .HasOperators("varchar_pattern_ops");

    // 3. Составной индекс: Фильтр по режиму (Status) + Дефолтная сортировка (InsertedDate DESC)
    builder.HasIndex(x => new { x.Status, x.InsertedDate })
        .IsDescending(false, true)
        .HasDatabaseName("IX_Subdivisions_Status_InsertedDate");
  }
}
