using Guard.Core.Identity;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Http;

internal static class OwnPasswordBoundaryTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var contexts = new HttpContextAccessor();
    var service = new OwnPasswordService(contexts, null!, null!, Microsoft.Extensions.Logging.Abstractions.NullLogger<OwnPasswordService>.Instance);
    check(await service.GetStateAsync() == OwnPasswordState.MissingUser,
      "own password adapter rejects missing HTTP context before accessing Identity");
    contexts.HttpContext = new DefaultHttpContext();
    check((await service.ChangeAsync("old", "new", "new")).Status == OwnPasswordChangeStatus.Unauthorized,
      "own password adapter rejects anonymous calls before accessing Identity");
    using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
    await PostgresTests.ThrowsAsync<OperationCanceledException>(() => service.GetStateAsync(cancelled.Token), check,
      "own password read respects cancellation before accessing Identity");
    await PostgresTests.ThrowsAsync<OperationCanceledException>(() => service.ChangeAsync("old", "new", "new", cancelled.Token), check,
      "own password change respects cancellation before accessing Identity");
  }
}
