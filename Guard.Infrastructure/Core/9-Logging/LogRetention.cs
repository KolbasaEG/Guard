using Guard.Core.Contexts;
using Guard.Core.Enums;
using Guard.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Logging;

public sealed class LogRetention(IDbContextFactory<ApplicationDbContext> factory,
  string directory = "logs", AuditJournal? journal = null, AuditHealth? health = null, InternalLogWriter? diagnostics = null)
{
  private int? lastYears;
  public async Task CleanAsync(CancellationToken ct)
  {
    var errors = new List<string>();
    try {
      await using var settings = await factory.CreateDbContextAsync(ct);
      var years = await settings.AuditSettings.Where(s => s.Id == 1).Select(s => (int?)s.RetentionYears).SingleAsync(ct);
      if (years is null or < 1 or > 30) throw new InvalidOperationException("Некорректный срок хранения аудита.");
      lastYears = years;
    } catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
    catch (Exception error) { errors.Add("Configuration:" + error.GetType().Name); }
    if (!lastYears.HasValue) { health?.Cleanup(string.Join(";", errors)); return; }
    var cutoff = DateTime.UtcNow.AddYears(-lastYears.Value);
    await RunAsync("Audit", async () => {
      await using var db = await factory.CreateDbContextAsync(ct); db.Database.SetCommandTimeout(10);
      for (var batch = 0; batch < 10; batch++) {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var ids = db.AuditEvents.Where(e => e.OccurredAtUtc < cutoff && (!e.DeliveryRequired || e.SentAtUtc != null))
          .OrderBy(e => e.OccurredAtUtc).ThenBy(e => e.EventId).Take(1000).Select(e => e.EventId);
        var removed = await db.AuditEvents.Where(e => ids.Contains(e.EventId)).ExecuteDeleteAsync(ct);
        if (removed > 0 && journal != null) await journal.WriteInTransactionAsync(db,
          AuditEventData.Create(AuditEventType.AuditCleanupCompleted, null, details: $"Removed={removed}; CutoffUtc={cutoff:O}"), true, ct);
        await tx.CommitAsync(ct);
        if (removed < 1000) break;
      }
    });
    await RunAsync("Files", () => { CleanFiles(directory, cutoff); return Task.CompletedTask; });
    health?.Cleanup(errors.Count == 0 ? null : string.Join(";", errors));

    async Task RunAsync(string stage, Func<Task> action) {
      try { await action(); }
      catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
      catch (Exception error) {
        errors.Add(stage + ":" + error.GetType().Name);
        diagnostics?.Write($"AuditCleanupFailed Stage={stage} ErrorType={error.GetType().Name}");
      }
    }
  }
  internal static void CleanFiles(string directory, DateTime cutoff)
  {
    if (!Directory.Exists(directory)) return;
    foreach (var file in Directory.EnumerateFiles(directory)) {
      var name = Path.GetFileName(file);
      if (name.StartsWith("audit-", StringComparison.Ordinal) &&
        File.GetLastWriteTimeUtc(file) < cutoff) File.Delete(file);
    }
  }
}
