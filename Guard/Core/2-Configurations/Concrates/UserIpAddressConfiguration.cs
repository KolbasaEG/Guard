using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Guard.Core.Configurations;

/// <summary>
/// EF Core конфигурация join-сущности UserIpAddress.
/// </summary>
public class UserIpAddressConfiguration : IEntityTypeConfiguration<UserIpAddress>
{
  public void Configure(EntityTypeBuilder<UserIpAddress> builder)
  {
    // =====================================================
    // НАЗВАНИЕ ТАБЛИЦЫ: UserIpAddresses
    // ОПИСАНИЕ: Join-таблица связи многие-ко-многим между пользователями и IP-адресами.
    //           Хранит метаданные назначения (кто, когда, с какой целью назначил IP).
    // =====================================================
    builder.ToTable("UserIpAddresses", t => t.HasComment(@"Join-таблица связи многие-ко-многим между пользователями и IP-адресами. Хранит метаданные назначения (кто, когда, с какой целью назначил IP)."));

    builder.HasKey(x => x.Id);

    builder.HasIndex(x => new { x.UserId, x.IpAddressId }).IsUnique();
    builder.HasIndex(x => new { x.UserId, x.IsActive });
    builder.HasIndex(x => x.IpAddressId);
  }
}
