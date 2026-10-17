using Guard.Core.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Guard.Web.Maintenance;

public static class IpCatalogMaintenanceCommand
{
  public static async Task<bool> TryRunAsync(string[] args)
  {
    var prepare = args.Contains("--prepare-ip-catalog", StringComparer.Ordinal);
    var confirm = args.Contains("--confirm-maintenance", StringComparer.Ordinal);
    if (!prepare && !confirm) return false;
    if (!prepare || !confirm || args.Length != 2) {
      Console.Error.WriteLine("Для подготовки каталога остановите запись и укажите --prepare-ip-catalog --confirm-maintenance. Сначала примените миграции к выбранной БД.");
      Environment.ExitCode = 2;
      return true;
    }
    using var stopping = new CancellationTokenSource();
    ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; stopping.Cancel(); };
    Console.CancelKeyPress += cancel;
    try {
      // Configuration only: no host, database creation, migrations, logs or seeding.
      var builder = WebApplication.CreateBuilder(Array.Empty<string>());
      var connection = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Не задано подключение основной БД.");
      var services = new ServiceCollection();
      services.AddDbContextFactory<ApplicationDbContext>(o => o.UseNpgsql(connection));
      await using var provider = services.BuildServiceProvider();
      var maintenance = new IpCatalogKeyMaintenance(provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());
      long total = 0, invalid = 0;
      while (true) {
        var batch = await maintenance.PrepareBatchAsync(ct: stopping.Token);
        total += batch.Updated; invalid += batch.Invalid;
        if (batch.Updated == 0) break;
        Console.WriteLine($"Подготовлено за этот запуск: {total}; некорректных строк: {invalid}.");
      }
      Console.WriteLine($"Подготовка завершена. За этот запуск: {total}; некорректных строк: {invalid}. Исходные адреса и связи сохранены.");
    }
    catch (OperationCanceledException) when (stopping.IsCancellationRequested) {
      Console.Error.WriteLine("Подготовка прервана. Подтверждённые пакеты сохранены; повторите команду для продолжения.");
      Environment.ExitCode = 130;
    }
    catch (Exception ex) {
      Console.Error.WriteLine($"Подготовка не завершена ({ex.GetType().Name}). Проверьте миграции и подключение; повторный запуск продолжит обработку.");
      Environment.ExitCode = 1;
    }
    finally { Console.CancelKeyPress -= cancel; }
    return true;
  }
}
