using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Guard.Core.Contexts;

public static class DatabaseInitializer
{
  /// <summary>
  /// Универсальный метод: проверяет существование физической БД PostgreSQL по строки подключения и создает ее, если отсутствует.
  /// </summary>
  public static void EnsureDatabaseExists(string connectionString)
  {
    var builder = new NpgsqlConnectionStringBuilder(connectionString);
    var targetDatabaseName = builder.Database;

    if (string.IsNullOrEmpty(targetDatabaseName)) return;

    // Временно подключаемся к служебной базе "postgres"
    builder.Database = "postgres";

    using var systemConnection = new NpgsqlConnection(builder.ConnectionString);
    systemConnection.Open();

    using var checkDbCmd = systemConnection.CreateCommand();
    checkDbCmd.CommandText = "SELECT 1 FROM pg_database WHERE datname = @dbName;";
    checkDbCmd.Parameters.AddWithValue("dbName", targetDatabaseName);

    var dbExists = checkDbCmd.ExecuteScalar();

    if (dbExists == null || dbExists == DBNull.Value)
    {
      using var createDbCmd = systemConnection.CreateCommand();
      createDbCmd.CommandText = $"CREATE DATABASE \"{targetDatabaseName}\";";
      createDbCmd.ExecuteNonQuery();
    }
  }

  /// <summary>
  /// Создает физическую БД логов и гарантирует наличие структуры таблицы 'logs' до старта Serilog
  /// </summary>
  public static void EnsureLogsDatabaseExists(string logsConnectionString)
  {
    // 1. Создаем саму физическую БД (если ее нет)
    EnsureDatabaseExists(logsConnectionString);

    // 2. Создаем таблицу 'logs' (если ее нет)
    using var connection = new NpgsqlConnection(logsConnectionString);
    connection.Open();

    using var cmd = connection.CreateCommand();
    cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS public.logs (
                timestamp TIMESTAMP NOT NULL,
                level VARCHAR(50),
                layer VARCHAR(50),
                message TEXT,
                message_template TEXT,
                exception TEXT,
                properties JSONB,
                user_id VARCHAR(250),
                ip_address VARCHAR(50)
            );";
    cmd.ExecuteNonQuery();
  }

  /// <summary>
  /// Накатывает последние EF Core миграции для бизнес-контекстов
  /// </summary>
  public static async Task ApplyMigrationsAsync(IServiceProvider serviceProvider)
  {
    using var scope = serviceProvider.CreateScope();

    // 1. Применяем миграции для основной БД (ApplicationDbContext)
    var appContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await appContext.Database.MigrateAsync();

    // 2. Третья БД (добавьте при появлении)
    // var thirdContext = scope.ServiceProvider.GetRequiredService<ThirdDbContext>();
    // await thirdContext.Database.MigrateAsync();
  }
}