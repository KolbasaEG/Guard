using Guard.Web.Maintenance;

internal static class IpCatalogMaintenanceCommandTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    check(!await IpCatalogMaintenanceCommand.TryRunAsync([]), "normal startup skips IP maintenance command");
    var previousExit = Environment.ExitCode;
    var previousError = Console.Error;
    using var error = new StringWriter();
    try {
      Console.SetError(error);
      foreach (var args in new[] { new[] { "--prepare-ip-catalog" }, new[] { "--confirm-maintenance" },
        new[] { "--prepare-ip-catalog", "--confirm-maintenance", "unexpected" } }) {
        check(await IpCatalogMaintenanceCommand.TryRunAsync(args) && Environment.ExitCode == 2,
          "incomplete maintenance invocation exits before host or database initialization");
      }
    }
    finally { Environment.ExitCode = previousExit; Console.SetError(previousError); }
  }
}
