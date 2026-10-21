using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using System.Text;

namespace Guard.Components.Pages.Logs;

public partial class TechnicalLogExportForm : IDisposable
{
  [Inject] public ITechnicalLogService Service { get; set; } = default!;
  [Inject] public ILogger<TechnicalLogExportForm> Logger { get; set; } = default!;
  private DateTime from = DateTime.UtcNow.AddHours(-1), to = DateTime.UtcNow;
  private string correlation = "", session = "", source = "Database", afterCursor = "";
  private string? level, preview, error;
  private bool busy, disposed;
  private readonly CancellationTokenSource stopping = new();
  private static readonly string[] levels = ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];
  private static readonly Choice[] sources = [new("Database", "База данных"), new("LocalBuffer", "Локальный буфер")];
  private sealed record Choice(string Value, string Text);
  private async Task Preview()
  {
    if (busy || disposed) return;
    busy = true; preview = null; error = null;
    try {
      Guid? sessionId = string.IsNullOrWhiteSpace(session) ? null : Guid.Parse(session);
      var package = await Service.ExportAsync(new(DateTime.SpecifyKind(from, DateTimeKind.Utc), DateTime.SpecifyKind(to, DateTimeKind.Utc),
        correlation, sessionId, TechnicalLogSource.LocalBuffer, level, string.IsNullOrEmpty(afterCursor) ? null : afterCursor), stopping.Token);
      if (!disposed) preview = string.Join('\n', Encoding.UTF8.GetString(package.Content.AsSpan(0, Math.Min(package.Content.Length, 65536))).Split('\n').Take(20));
    } catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
    catch (Exception ex) { if (!disposed) { Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(Logger, ex, "Просмотр локального буфера технических логов"); error = ex is FormatException ? "Проверьте ID диагностической сессии." : UserOperationErrors.Message(ex); } }
    finally { busy = false; }
  }
  public void Dispose() { disposed = true; stopping.Cancel(); stopping.Dispose(); }
}
