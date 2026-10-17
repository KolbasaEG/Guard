namespace Guard.Core.Services;
public interface IAccessChangeNotifier
{
  IDisposable Subscribe(string userId, Func<Task> refresh);
  Task PublishAsync(IEnumerable<string> userIds);
}
