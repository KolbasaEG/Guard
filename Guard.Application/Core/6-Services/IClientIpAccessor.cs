namespace Guard.Core.Services;

/// <summary>Доверенный адрес текущего подключения; null вне запроса или без адреса. Не подтверждает доступ.</summary>
public interface IClientIpAccessor
{
  string? GetClientIp();
}
