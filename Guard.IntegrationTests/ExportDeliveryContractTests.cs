using Guard.Core.Contexts;
using Guard.Core.Enums;
using Guard.Core.Services;
using Guard.Core.Services.Exports;
using Microsoft.EntityFrameworkCore;

internal static partial class ExportIntegrationTests
{
  private static async Task CheckDeliveryContractAsync(IDbContextFactory<ApplicationDbContext> factory,
    DataExportService exports, ExportProcessor processor, IExportDeliveryService delivery, Action<bool, string> check)
  {
    foreach (var outcome in new[] { "success", "io", "cancelled" }) {
      var id = await exports.CreateAsync(new(ExportArea.Personals, ExportFormat.Excel, IncludeChildren: true));
      await processor.RunNextAsync(default);
      using var cancellation = new CancellationTokenSource();
      async Task Transfer(ExportDeliveryFile file, Stream stream, CancellationToken ct) {
        await using var db = await factory.CreateDbContextAsync();
        check(ct == cancellation.Token && file.Size > 0 && !string.IsNullOrEmpty(file.FileName) && !string.IsNullOrEmpty(file.ContentType),
          "delivery callback receives original cancellation and independent file metadata");
        check(await db.ExportJobs.AnyAsync(j => j.Id == id && j.State == ExportJobState.Delivering) &&
          await db.ExportDeliveryAttempts.CountAsync(a => a.JobId == id && a.CompletedAtUtc == null) == 1,
          "callback sees committed one-time claim through independent context before receiving bytes");
        if (outcome == "cancelled") { cancellation.Cancel(); ct.ThrowIfCancellationRequested(); }
        if (outcome == "io") throw new IOException("Synthetic delivery sink failure");
        using var target = new MemoryStream();
        await stream.CopyToAsync(target, ct); await target.FlushAsync(ct);
        var artifact = await db.ExportArtifacts.AsNoTracking().SingleAsync(a => a.JobId == id);
        check(target.Length == file.Size && Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(target.ToArray())) == artifact.Sha256,
          "independent delivery sink receives exact verified artifact bytes");
      }
      if (outcome == "success") await delivery.DeliverAsync(id, Transfer, cancellation.Token);
      else if (outcome == "io")
        await PostgresTests.ThrowsAsync<IOException>(() => delivery.DeliverAsync(id, Transfer, cancellation.Token), check, "sink IO failure propagates");
      else
        await PostgresTests.ThrowsAsync<OperationCanceledException>(() => delivery.DeliverAsync(id, Transfer, cancellation.Token), check, "sink cancellation propagates");
      await using (var db = await factory.CreateDbContextAsync()) {
        var job = await db.ExportJobs.AsNoTracking().SingleAsync(j => j.Id == id);
        var attempt = await db.ExportDeliveryAttempts.AsNoTracking().SingleAsync(a => a.JobId == id);
        check(job.State == (outcome == "success" ? ExportJobState.Delivered : ExportJobState.Failed) &&
          attempt.Succeeded == (outcome == "success") && attempt.CompletedAtUtc != null,
          "delivery result commits even when original token is cancelled");
      }
      var repeated = false;
      await PostgresTests.ThrowsAsync<EntityRuleException>(() => delivery.DeliverAsync(id, (_, _, _) => {
        repeated = true; return Task.CompletedTask;
      }), check, "consumed delivery cannot be repeated after success or failure");
      check(!repeated, "repeat denial never invokes delivery sink");
    }
  }
}
