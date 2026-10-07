namespace Guard.Core.Identity;
// Scoped: общий сигнал для компонентов одного circuit и его обработчика подключения.
public sealed class AccessRefreshCoordinator
{
  public event Func<Task>? RefreshRequested;
  public Task RequestAsync() => Task.WhenAll(RefreshRequested?.GetInvocationList().Cast<Func<Task>>().Select(callback => callback()) ?? []);
}
