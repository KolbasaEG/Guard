using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Guard.Core.Configurations.Concrates
{
  public class ClassifierConfiguration : IEntityTypeConfiguration<Classifier>
  {
    public void Configure(EntityTypeBuilder<Classifier> builder)
    {
      builder.ToTable("Classifiers", t => t.HasComment("Универсальный справочник классификаторов системы"));

      // Первичный ключ (long)
      builder.HasKey(c => c.Id);
      builder.Property(c => c.Id)
          .ValueGeneratedOnAdd()
          .HasComment("Уникальный идентификатор записи классификатора");

      builder.Property(c => c.Type)
          .HasConversion<int>()
          .IsRequired()
          .HasComment("Тип классификатора (enum ClassifierType)");

      builder.Property(c => c.Code)
          .IsRequired()
          .HasComment("Код значения внутри конкретного классификатора (1, 2, 3...)");

      builder.Property(c => c.ClassifierName)
          .HasMaxLength(256)
          .IsRequired()
          .HasComment("Наименование группы классификатора (например: 'Тип документа')");

      builder.Property(c => c.Value)
          .HasMaxLength(500)
          .IsRequired()
          .HasComment("Отображаемое значение элемента (например: 'Паспорт', 'Водительское удостоверение')");

      builder.Property(c => c.IsActive)
          .HasDefaultValue(true)
          .HasComment("Флаг актуальности записи (true — активен, false — неактивен)");

      builder.Property(c => c.UpdatedAt)
          .HasColumnType("timestamp with time zone")
          .HasDefaultValueSql("CURRENT_TIMESTAMP")
          .HasComment("Дата и время последнего обновления записи (UTC)");

      // Составной уникальный индекс по комбинации (Type, Code)
      builder.HasIndex(c => new { c.Type, c.Code })
          .IsUnique();

      // Индекс по типам для быстрых выборок конкретных справочников
      builder.HasIndex(c => c.Type);
    }
  }
}
