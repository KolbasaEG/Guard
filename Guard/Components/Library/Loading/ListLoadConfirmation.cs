namespace Guard.Components.Library.Loading;

// Подтверждение относится только к загрузке, начатой конкретным обновлением таблицы.
public sealed class ListLoadConfirmation
{
  private long current;
  private long successful;
  public long Begin() => ++current;
  public void Confirm(long request)
  {
    if (request == current) successful = request;
  }
  public async Task<bool> ReloadAsync(Func<Task> reload)
  {
    var expected = current + 1;
    await reload();
    return current == expected && successful == expected;
  }
}
