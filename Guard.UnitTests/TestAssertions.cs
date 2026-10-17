internal static class TestAssertions
{
  public static async Task ThrowsAsync<T>(Func<Task> action, Action<bool, string> check, string scenario) where T : Exception
  {
    try { await action(); }
    catch (T) { check(true, scenario); return; }
    check(false, scenario);
  }
}
