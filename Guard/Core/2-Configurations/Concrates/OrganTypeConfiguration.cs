using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Guard.Core.Configurations;

public class OrganTypeConfiguration : IEntityTypeConfiguration<OrganType>
{
  public void Configure(EntityTypeBuilder<OrganType> builder)
  {
    builder.ToTable("OrganTypes", t => t.HasComment("Справочник типов органов"));

    builder.HasKey(o => o.Id);

    builder.Property(o => o.Id)
        .ValueGeneratedNever()
        .HasComment("Идентификатор типа органа");

    builder.Property(o => o.ClassifierType)
        .HasDefaultValue(906)
        .IsRequired()
        .HasComment("Тип классификатора (906)");

    builder.Property(o => o.Code)
        .IsRequired()
        .HasComment("Код элемента классификатора");

    builder.Property(o => o.Name)
        .HasMaxLength(500)
        .IsRequired()
        .HasComment("Наименование типа органа");

    // Составной уникальный индекс (Id, Code) — целевой ключ для связи с Subdivisions
    builder.HasIndex(o => new { o.Id, o.Code })
        .IsUnique()
        .HasDatabaseName("UQ_OrganTypes_Id_Code");

    // Составной внешний ключ на Classifiers (int, int) -> (int, int)
    builder.HasOne(o => o.Classifier)
        .WithMany()
        .HasForeignKey(o => new { o.ClassifierType, o.Code })
        .HasPrincipalKey(c => new { c.Type, c.Code })
        .OnDelete(DeleteBehavior.Restrict);
  }
}