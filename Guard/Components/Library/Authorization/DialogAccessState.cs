
namespace Guard.Components.Library.Authorization;
// Контракт UI: библиотека не знает прикладных сервисов и источника прав.
public sealed class DialogAccessState(bool canView, bool canWrite, string? message, Func<Task> refreshAsync)
{
  public bool CanView { get; set; } = canView;
  public bool CanWrite { get; set; } = canWrite;
  public string? Message { get; set; } = message;
  public Func<Task> RefreshAsync { get; } = refreshAsync;
}
