using System.Text.Json;
using Guard.Core.Contexts;
using Guard.Core.Enums;
using Guard.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Guard.Core.Logging;

/// <summary>Обязательный локальный журнал. Сеть SIEM не участвует в бизнес-транзакции.</summary>
public sealed class AuditJournal : IDisposable, IOperationAvailability
{
  internal const long QueueLock = 73460220;
  private readonly NpgsqlDataSource source;
  private readonly InternalLogWriter diagnostics;
  private readonly AuditHealth health;
  private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

  public AuditJournal(string connectionString, InternalLogWriter diagnostics, AuditHealth health)
  {
    source = NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder(connectionString) { Timeout = 3, CommandTimeout = 5 }.ConnectionString);
    this.diagnostics = diagnostics; this.health = health;
  }

  public void Write(AuditEventData entry) => WriteAsync(entry).GetAwaiter().GetResult();

  public async Task WriteAsync(AuditEventData entry, CancellationToken ct = default)
  {
    try {
      await using var connection = await source.OpenConnectionAsync(ct);
      await using var tx = await connection.BeginTransactionAsync(ct);
      await InsertAsync(connection, tx, entry, IsServiceEvent(entry), ct);
      await CommitAsync(tx, ct);
      health.Stored();
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
    catch (OperationCommitUnknownException) { throw; }
    catch (AuditUnavailableException) { throw; }
    catch (Exception error) { throw Failed(entry.EventId, error); }
  }

  internal async Task WriteInTransactionAsync(ApplicationDbContext db, AuditEventData entry, bool recovery, CancellationToken ct)
  {
    if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Аудит требует транзакцию операции.");
    try {
      await InsertAsync((NpgsqlConnection)db.Database.GetDbConnection(),
        (NpgsqlTransaction)db.Database.CurrentTransaction.GetDbTransaction(), entry, recovery, ct);
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
    catch (AuditUnavailableException) { throw; }
    catch (Exception error) { throw Failed(entry.EventId, error); }
  }

  internal async Task CheckTransactionAsync(ApplicationDbContext db, CancellationToken ct)
  {
    try {
    var connection = (NpgsqlConnection)db.Database.GetDbConnection();
    var tx = (NpgsqlTransaction)(db.Database.CurrentTransaction?.GetDbTransaction()
      ?? throw new InvalidOperationException("Проверка записи требует транзакцию."));
    await LockAsync(connection, tx, ct);
    var configuration = await ReadAsync(connection, tx, ct);
    var queue = await ReadQueueAsync(connection, tx, ct);
    Demand(configuration, queue, 0, 0);
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
    catch (AuditUnavailableException) { throw; }
    catch (Exception error) { throw Failed(null, error); }
  }

  public async Task RequireWorkingAsync(CancellationToken ct = default)
  {
    var state = await GetStateAsync(ct);
    if (!state.Available) throw new AuditUnavailableException(state.Code, state.Message);
  }

  public async Task<AuditConfiguration> GetConfigurationAsync(CancellationToken ct = default)
  {
    await using var connection = await source.OpenConnectionAsync(ct);
    return await ReadAsync(connection, null, ct);
  }

  public async Task<AuditSystemState> GetStateAsync(CancellationToken ct = default)
  {
    try {
      await using var connection = await source.OpenConnectionAsync(ct);
      await using var tx = await connection.BeginTransactionAsync(ct);
      // Проверяем также commit. Проба удаляется в той же транзакции: чтения не создают историю аудита.
      await using (var probe = new NpgsqlCommand("UPDATE \"AuditSettings\" SET \"Id\" = \"Id\" WHERE \"Id\" = 1", connection, tx)) {
        if (await probe.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("Нет настроек аудита.");
      }
      var probeId = Guid.NewGuid();
      await using (var probe = new NpgsqlCommand("INSERT INTO \"AuditEvents\" (\"EventId\",\"OccurredAtUtc\",\"SchemaVersion\",\"EventType\",\"ActorKind\",\"Outcome\",\"Attempts\",\"NextAttemptAtUtc\",\"DeliveryRequired\",\"DestinationVersion\",\"PayloadBytes\") VALUES (@id,now(),2,36,'system','probe',0,now(),false,@version,0)", connection, tx)) {
        probe.Parameters.AddWithValue("id", probeId); probe.Parameters.AddWithValue("version", Guid.Empty);
        await probe.ExecuteNonQueryAsync(ct);
      }
      var configuration = await ReadAsync(connection, tx, ct);
      var queue = await ReadQueueAsync(connection, tx, ct);
      DateTime? sent; DateTime? worker;
      await using (var command = new NpgsqlCommand("SELECT (SELECT max(\"SentAtUtc\") FROM \"AuditEvents\"), \"WorkerCompletedAtUtc\" FROM \"AuditSettings\" WHERE \"Id\"=1", connection, tx)) {
        await using var reader = await command.ExecuteReaderAsync(ct); await reader.ReadAsync(ct);
        sent = reader.IsDBNull(0) ? null : reader.GetDateTime(0); worker = reader.IsDBNull(1) ? null : reader.GetDateTime(1);
      }
      await using (var remove = new NpgsqlCommand("DELETE FROM \"AuditEvents\" WHERE \"EventId\"=@id", connection, tx)) {
        remove.Parameters.AddWithValue("id", probeId); await remove.ExecuteNonQueryAsync(ct);
      }
      await CommitAsync(tx, ct); health.StorageChecked();
      var code = "Ready"; var message = configuration.Mode == AuditMode.Local ? "Локальный аудит исправен." : "Аудит исправен.";
      if (GetBlocker(configuration, queue, 0, 0) is { } blocker) { code = blocker.Code; message = blocker.Message; }
      if (code == "Ready" && configuration.Mode == AuditMode.Siem && queue.Count > 0) message = "Локальный аудит исправен. Есть события, ожидающие отправки.";
      return new(configuration.Mode, code == "Ready", code, message, DateTime.UtcNow, queue.Count, queue.Bytes,
        queue.Oldest, sent, worker, health.LastStorageSuccessUtc, health.LastFailureUtc, health.LastFailure,
        health.StorageFailures, health.LastCleanupUtc, health.CleanupError, health.CertificateExpiresAtUtc,
        configuration.MaxQueueAgeMinutes, configuration.MaxQueueBytes, configuration.MaxQueueCount, configuration.Version);
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
    catch (Exception error) {
      Failed(null, error);
      return new(AuditMode.Local, false, "StorageUnavailable", "Обязательный локальный аудит недоступен. Рабочие операции запрещены.",
        DateTime.UtcNow, 0, 0, null, null, null, health.LastStorageSuccessUtc, health.LastFailureUtc,
        health.LastFailure, health.StorageFailures, health.LastCleanupUtc, health.CleanupError);
    }
  }

  internal async Task SaveConfigurationAsync(AuditConfiguration input, string actor, bool transferPending,
    Func<CancellationToken, Task<string>> recheckRoot, CancellationToken ct)
  {
    await using var connection = await source.OpenConnectionAsync(ct);
    await using var tx = await connection.BeginTransactionAsync(ct);
    await using (var security = new NpgsqlCommand("SELECT pg_advisory_xact_lock(74162002)", connection, tx))
      await security.ExecuteNonQueryAsync(ct);
    await LockAsync(connection, tx, ct);
    if (await recheckRoot(ct) != actor) throw new UnauthorizedAccessException("Сессия Root изменилась.");
    var previous = await ReadAsync(connection, tx, ct);
    if (previous.Version != input.Version) throw new InvalidOperationException("Настройки изменены. Обновите страницу.");
    var changedDestination = previous.Host != input.Host || previous.Port != input.Port || previous.CaCertificatePem != input.CaCertificatePem;
    var pending = await ReadQueueAsync(connection, tx, ct);
    if (changedDestination && pending.Count > 0 && !transferPending)
      throw new ArgumentException("Есть обязательная очередь. Подтвердите её перенос на новое назначение.");
    if (changedDestination || previous.Mode != input.Mode) {
      await using var leased = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM \"AuditEvents\" WHERE \"LeaseUntilUtc\">now() AND \"SentAtUtc\" IS NULL)", connection, tx);
      if ((bool)(await leased.ExecuteScalarAsync(ct))!) throw new InvalidOperationException("Идёт отправка. Повторите смену режима или назначения после завершения попытки; после сбоя процесса аренда истекает через 2 минуты.");
    }
    var version = Guid.NewGuid();
    await using (var update = new NpgsqlCommand("""
      UPDATE "AuditSettings" SET "Mode"=@mode,"Configured"=true,"RetentionYears"=@years,
        "MaxQueueAgeMinutes"=@age,"MaxQueueBytes"=@bytes,"MaxQueueCount"=@count,"Host"=@host,"Port"=@port,
        "CaCertificatePem"=@ca,"Version"=@version,"ModifiedBy"=@actor,"ModifiedAtUtc"=now() WHERE "Id"=1
      """, connection, tx)) {
      update.Parameters.AddWithValue("mode", (int)input.Mode); update.Parameters.AddWithValue("years", input.RetentionYears);
      update.Parameters.AddWithValue("age", input.MaxQueueAgeMinutes); update.Parameters.AddWithValue("bytes", input.MaxQueueBytes);
      update.Parameters.AddWithValue("count", input.MaxQueueCount); update.Parameters.AddWithValue("host", input.Host);
      update.Parameters.AddWithValue("port", input.Port); update.Parameters.AddWithValue("version", version);
      update.Parameters.AddWithValue("actor", actor); update.Parameters.AddWithValue("ca", NpgsqlTypes.NpgsqlDbType.Text, (object?)input.CaCertificatePem ?? DBNull.Value);
      await update.ExecuteNonQueryAsync(ct);
    }
    if (changedDestination) {
      await using var move = new NpgsqlCommand("UPDATE \"AuditEvents\" SET \"DestinationVersion\"=@version WHERE \"DeliveryRequired\" AND \"SentAtUtc\" IS NULL", connection, tx);
      move.Parameters.AddWithValue("version", version); await move.ExecuteNonQueryAsync(ct);
    }
    var change = AuditEventData.Create(AuditEventType.AuditConfigurationChanged, actor, details: JsonSerializer.Serialize(new {
      Before = Safe(previous), After = Safe(input), CertificateChanged = previous.CaCertificatePem != input.CaCertificatePem,
      TransferPending = transferPending, Pending = pending.Count
    })) with { ObjectType = "AuditSettings", ObjectId = "1" };
    await InsertAsync(connection, tx, change, true, ct);
    await CommitAsync(tx, ct); health.Stored();
  }

  private static object Safe(AuditConfiguration c) => new { c.Mode, c.RetentionYears, c.MaxQueueAgeMinutes,
    c.MaxQueueBytes, c.MaxQueueCount, c.Host, c.Port };

  private async Task InsertAsync(NpgsqlConnection connection, NpgsqlTransaction tx, AuditEventData entry, bool recovery, CancellationToken ct)
  {
    entry = entry.Normalize(); await LockAsync(connection, tx, ct);
    await using (var duplicate = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM \"AuditEvents\" WHERE \"EventId\"=@id)", connection, tx)) {
      duplicate.Parameters.AddWithValue("id", entry.EventId); if ((bool)(await duplicate.ExecuteScalarAsync(ct))!) return;
    }
    var configuration = await ReadAsync(connection, tx, ct);
    var size = JsonSerializer.SerializeToUtf8Bytes(entry).Length;
    if (!recovery) Demand(configuration, await ReadQueueAsync(connection, tx, ct), 1, size);
    else if (configuration.Mode == AuditMode.Siem && entry.EventType is not (AuditEventType.AuditConfigurationChanged or AuditEventType.AuditCleanupCompleted)) {
      var reserve = await ReadQueueAsync(connection, tx, ct);
      var extra = Math.Max(1048576L, configuration.MaxQueueBytes / 100);
      if (reserve.Bytes > configuration.MaxQueueBytes && reserve.Bytes - configuration.MaxQueueBytes + size > extra ||
        configuration.MaxQueueCount > 0 && reserve.Count >= (long)configuration.MaxQueueCount + 1000)
        throw new AuditUnavailableException("ServiceReserveExceeded", "Служебный резерв аудита исчерпан. Доступны выход и восстановление Root.");
    }
    var now = DateTime.UtcNow;
    await using var command = new NpgsqlCommand("""
      INSERT INTO "AuditEvents" ("EventId","OccurredAtUtc","SchemaVersion","EventType","Actor","ActorKind","Outcome",
        "ClientIp","CorrelationId","ObjectType","ObjectId","Details","ReasonCode","AttemptedAccount","Attempts",
        "NextAttemptAtUtc","DeliveryRequired","QueuedAtUtc","DestinationVersion","PayloadBytes")
      VALUES (@id,@time,2,@type,@actor,@kind,@outcome,@ip,@correlation,@objectType,@objectId,@details,@reason,@account,
        0,@now,@required,@queued,@version,@bytes) ON CONFLICT ("EventId") DO NOTHING
      """, connection, tx);
    command.Parameters.AddWithValue("id", entry.EventId); command.Parameters.AddWithValue("time", entry.OccurredAtUtc);
    command.Parameters.AddWithValue("type", (int)entry.EventType); command.Parameters.AddWithValue("now", now);
    command.Parameters.AddWithValue("required", configuration.Mode == AuditMode.Siem);
    command.Parameters.AddWithValue("queued", NpgsqlTypes.NpgsqlDbType.TimestampTz, configuration.Mode == AuditMode.Siem ? (object)now : DBNull.Value);
    command.Parameters.AddWithValue("version", configuration.Version); command.Parameters.AddWithValue("bytes", size);
    foreach (var (name, value) in new[] { ("actor", entry.Actor), ("kind", entry.ActorKind), ("outcome", entry.Outcome),
      ("ip", entry.ClientIp), ("correlation", entry.CorrelationId), ("objectType", entry.ObjectType), ("objectId", entry.ObjectId),
      ("details", entry.Details), ("reason", entry.ReasonCode), ("account", entry.AttemptedAccount) })
      command.Parameters.AddWithValue(name, NpgsqlTypes.NpgsqlDbType.Text, (object?)value ?? DBNull.Value);
    await command.ExecuteNonQueryAsync(ct);
  }

  internal static async Task LockAsync(NpgsqlConnection connection, NpgsqlTransaction tx, CancellationToken ct)
  {
    await using var command = new NpgsqlCommand($"SELECT pg_advisory_xact_lock({QueueLock})", connection, tx);
    await command.ExecuteNonQueryAsync(ct);
  }
  private static async Task CommitAsync(NpgsqlTransaction tx, CancellationToken ct)
  {
    try { await tx.CommitAsync(ct); }
    catch (Exception error) when (error is NpgsqlException or IOException or TimeoutException or OperationCanceledException) {
      throw new OperationCommitUnknownException(AuditOperationContext.Current?.CorrelationId, error);
    }
  }
  private static async Task<AuditConfiguration> ReadAsync(NpgsqlConnection connection, NpgsqlTransaction? tx, CancellationToken ct)
  {
    await using var command = new NpgsqlCommand("SELECT row_to_json(s)::text FROM \"AuditSettings\" s WHERE \"Id\"=1", connection, tx);
    var result = JsonSerializer.Deserialize<AuditConfiguration>((string?)await command.ExecuteScalarAsync(ct)
      ?? throw new InvalidOperationException("Настройки аудита отсутствуют."), Json)!;
    result.ModifiedAtUtc = result.ModifiedAtUtc?.ToUniversalTime();
    return result;
  }
  private sealed record Queue(long Count, long Bytes, DateTime? Oldest);
  private static async Task<Queue> ReadQueueAsync(NpgsqlConnection connection, NpgsqlTransaction? tx, CancellationToken ct)
  {
    await using var command = new NpgsqlCommand("SELECT count(*),COALESCE(sum(\"PayloadBytes\"),0)::bigint,min(\"QueuedAtUtc\") FROM \"AuditEvents\" WHERE \"DeliveryRequired\" AND \"SentAtUtc\" IS NULL", connection, tx);
    await using var reader = await command.ExecuteReaderAsync(ct); await reader.ReadAsync(ct);
    return new(reader.GetInt64(0), reader.GetInt64(1), reader.IsDBNull(2) ? null : reader.GetDateTime(2));
  }
  private static void Demand(AuditConfiguration c, Queue queue, int count, int bytes)
  {
    if (GetBlocker(c, queue, count, bytes) is { } blocker)
      throw new AuditUnavailableException(blocker.Code, blocker.Message);
  }
  private static (string Code, string Message)? GetBlocker(AuditConfiguration c, Queue queue, int count, int bytes)
  {
    if (!c.Configured) return ("NotConfigured", "Root должен выбрать и сохранить режим аудита.");
    if (c.Mode is not (AuditMode.Local or AuditMode.Siem) || c.RetentionYears is < 1 or > 30)
      return ("InvalidConfiguration", "Настройки аудита некорректны. Требуется восстановление Root.");
    if (c.Mode == AuditMode.Local) return null;
    if (c.MaxQueueAgeMinutes <= 0 || c.MaxQueueBytes <= 0) return ("InvalidConfiguration", "Не заданы обязательные пределы очереди SIEM.");
    if (queue.Oldest.HasValue && DateTime.UtcNow - queue.Oldest.Value > TimeSpan.FromMinutes(c.MaxQueueAgeMinutes))
      return ("QueueAgeExceeded", "Превышен максимальный возраст очереди SIEM. Рабочие операции запрещены.");
    if (queue.Bytes + bytes > c.MaxQueueBytes || c.MaxQueueCount > 0 && queue.Count + count > c.MaxQueueCount)
      return ("QueueVolumeExceeded", "Превышен допустимый объём очереди SIEM. Рабочие операции запрещены.");
    return null;
  }
  internal AuditUnavailableException Failed(Guid? id, Exception error)
  {
    var reason = error is PostgresException postgres ? $"PostgresException; SQLSTATE={postgres.SqlState}; Severity={postgres.InvariantSeverity}"
      : error.GetType().Name;
    health.StoreFailed(reason);
    diagnostics.Write($"AuditStorageFailed EventId={id} ErrorType={reason}");
    return new("StorageUnavailable", "Обязательный аудит недоступен. Операция не разрешена.", error);
  }
  internal static bool IsServiceEvent(AuditEventData e) => e.Outcome != "success" || e.EventType is
    AuditEventType.Logout or AuditEventType.SessionRevoked or AuditEventType.AuditStateChanged or
    AuditEventType.AuditConnectionTest or AuditEventType.AuditConfigurationChanged or AuditEventType.AuditCleanupCompleted or
    AuditEventType.ExportDeliveryCompleted or AuditEventType.ExportExpired;
  public void Dispose() => source.Dispose();
}
