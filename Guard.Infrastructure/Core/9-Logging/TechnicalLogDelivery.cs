using Npgsql;
using NpgsqlTypes;

namespace Guard.Core.Logging;

public sealed class TechnicalLogDelivery(string connectionString)
{
  public async Task DeliverAsync(IReadOnlyList<TechnicalLogRecord> records, CancellationToken ct)
  {
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync(ct);
    await using var tx = await connection.BeginTransactionAsync(ct);
    foreach (var record in records) await InsertAsync(connection, tx, record, ct);
    await tx.CommitAsync(ct);
  }
  public async Task ProbeAsync(CancellationToken ct)
  {
    await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(ct);
    await using var tx = await connection.BeginTransactionAsync(ct);
    // Проверяется право INSERT и текущая схема. Контрольное событие откатывается и не попадает в журнал.
    await InsertAsync(connection, tx, new(Guid.NewGuid(), DateTime.UtcNow, "Information", "System", "WriteProbe", "WriteProbe",
      null, "{}", null, null, null, "Guard.Logging.Probe", null, null, "probe", "probe", "probe"), ct);
    await tx.RollbackAsync(ct);
  }
  private static async Task InsertAsync(NpgsqlConnection connection, NpgsqlTransaction tx, TechnicalLogRecord record, CancellationToken ct)
  {
    await using var command = new NpgsqlCommand("""
      INSERT INTO public.logs(id,timestamp,level,layer,message,message_template,exception,properties,user_id,ip_address,
        correlation_id,source_context,diagnostic_session_id,run_id,fingerprint,application_version,instance_id)
      VALUES (@id,@time,@level,@layer,@message,@template,@exception,@properties,@user,@ip,@correlation,@source,@session,@run,@fingerprint,@version,@instance)
      ON CONFLICT (id) DO NOTHING
      """, connection, tx) { CommandTimeout = 5 };
    command.Parameters.AddWithValue("id", record.Id);
    command.Parameters.AddWithValue("time", NpgsqlDbType.Timestamp, DateTime.SpecifyKind(record.Timestamp, DateTimeKind.Unspecified));
    Add("level", record.Level); Add("layer", record.Layer); Add("message", record.Message); Add("template", record.MessageTemplate);
    Add("exception", record.Exception); Add("properties", record.Properties, NpgsqlDbType.Jsonb); Add("user", record.UserId);
    Add("ip", record.ClientIp); Add("correlation", record.CorrelationId); Add("source", record.SourceContext);
    Add("session", record.DiagnosticSessionId, NpgsqlDbType.Uuid); Add("run", record.RunId); Add("fingerprint", record.Fingerprint);
    Add("version", record.ApplicationVersion); Add("instance", record.InstanceId);
    await command.ExecuteNonQueryAsync(ct);
    void Add(string name, object? value, NpgsqlDbType type = NpgsqlDbType.Text) => command.Parameters.AddWithValue(name, type, value ?? DBNull.Value);
  }
  public async Task CleanupAsync(DateTime cutoff, CancellationToken ct)
  {
    await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(ct);
    for (var batch = 0; batch < 10; batch++) {
      await using var command = new NpgsqlCommand("""
        DELETE FROM public.logs WHERE ctid IN
          (SELECT ctid FROM public.logs WHERE timestamp < @cutoff ORDER BY timestamp LIMIT 10000)
        """, connection) { CommandTimeout = 5 };
      command.Parameters.AddWithValue("cutoff", NpgsqlDbType.Timestamp, DateTime.SpecifyKind(cutoff, DateTimeKind.Unspecified));
      if (await command.ExecuteNonQueryAsync(ct) < 10000) break;
    }
  }
}
