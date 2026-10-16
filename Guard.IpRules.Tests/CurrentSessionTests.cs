using System.Security.Claims;
using Guard.Components.Account;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Interceptors;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal static class CurrentSessionTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = Principal("http") } };
    var authentication = new Authentication { User = Principal("circuit") };
    var session = new CurrentSessionAdapter(authentication, http);
    check(await session.GetAsync() == new CurrentSession("circuit", "stamp-circuit"), "circuit identity takes priority over stale HTTP identity");
    authentication.User = Principal("next");
    check((await session.GetAsync())?.UserId == "next", "session reads fresh authentication without display initialization");
    authentication.User = new ClaimsPrincipal(new ClaimsIdentity());
    check(await session.GetAsync() == null, "anonymous circuit cannot fall back to authenticated HTTP");
    authentication.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "forged") ]));
    check(await session.GetAsync() == null, "unauthenticated identity claims do not identify an actor");
    authentication.User = new ClaimsPrincipal(new ClaimsIdentity([], "test"));
    check(await session.GetAsync() == null, "authenticated identity without ID has no actor");
    authentication.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "nostamp")], "test"));
    check(await session.GetAsync() == new CurrentSession("nostamp", null), "missing stamp is preserved for permission rejection, not invented");
    authentication.Failure = new InvalidOperationException("No circuit");
    check((await session.GetAsync())?.UserId == "http", "HTTP endpoint uses HTTP identity when circuit is unavailable");
    http.HttpContext = null;
    check(await session.GetAsync() == null, "background scope has no implicit actor");
    authentication.Failure = new IOException("authentication unavailable");
    await PostgresTests.ThrowsAsync<IOException>(() => session.GetAsync(), check, "authentication failures are not hidden by HTTP fallback");
    authentication.Failure = null;
    using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
    await PostgresTests.ThrowsAsync<OperationCanceledException>(() => session.GetAsync(cancelled.Token), check, "session respects cancelled token");

    var pending = new TaskCompletionSource<AuthenticationState>(TaskCreationOptions.RunContinuationsAsynchronously);
    authentication.Pending = pending.Task;
    using (var cancelWait = new CancellationTokenSource()) {
      var waiting = session.GetAsync(cancelWait.Token);
      cancelWait.Cancel();
      await PostgresTests.ThrowsAsync<OperationCanceledException>(() => waiting, check, "session wait can be cancelled without waiting for authentication");
    }
    var audit = new AuditSaveChangesInterceptor(session);
    var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql()
      .AddInterceptors(audit, new SuppressSave()).Options;
    await using var db = new ApplicationDbContext(options) { IsSynchronization = true };
    var entity = new Subdivision { Name = "Synthetic", Path = "/1/", CreatedBy = "client-supplied" };
    db.Add(entity);
    var saving = db.SaveChangesAsync();
    check(!saving.IsCompleted && entity.CreatedBy == "client-supplied", "audit awaits authentication before assigning author");
    pending.SetResult(new AuthenticationState(Principal("async-actor")));
    await saving;
    check(entity.CreatedBy == "async-actor" && entity.InsertedDate.Kind == DateTimeKind.Utc,
      "legacy async audit uses session without display DTO or database authorization recursion");
    check(entity.ModifiedBy == null && entity.LastModifiedDate == null, "insert audit clears client modification metadata");

    authentication.Pending = null;
    authentication.User = Principal("fresh-actor");
    db.Entry(entity).State = EntityState.Unchanged;
    entity.Name = "Changed";
    await db.SaveChangesAsync();
    check(entity.CreatedBy == "async-actor" && entity.ModifiedBy == "fresh-actor", "next save preserves creator and uses fresh actor");
    check(!db.Entry(entity).Property(e => e.CreatedBy).IsModified, "update protects original creator");

    authentication.User = new ClaimsPrincipal(new ClaimsIdentity());
    await db.SaveChangesAsync();
    check(entity.ModifiedBy == "system", "anonymous technical save uses explicit legacy system fallback");
    var permissions = new PermissionService(session, null!, null!, null!);
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => permissions.GetCurrentAsync(), check,
      "permission boundary rejects missing session before database or policy access");

    authentication.User = Principal("sync-actor");
    db.SaveChanges();
    check(entity.ModifiedBy == "sync-actor", "synchronous save supports already available authentication");
    pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
    authentication.Pending = pending.Task;
    try { db.SaveChanges(); check(false, "pending synchronous authentication must reject save"); }
    catch (InvalidOperationException) { check(entity.ModifiedBy == "sync-actor", "synchronous audit fails before save instead of blocking or using stale actor"); }
    pending.SetResult(new AuthenticationState(Principal("released")));
    authentication.Pending = null;
    await PostgresTests.ThrowsAsync<OperationCanceledException>(() => db.SaveChangesAsync(cancelled.Token), check, "cancelled save cannot relabel actor");
    check(entity.ModifiedBy == "sync-actor", "cancelled audit preserves existing metadata");
  }

  public static async Task RunIntegrationAsync(IDbContextFactory<ApplicationDbContext> factory, Action<bool, string> check)
  {
    await using var read = await factory.CreateDbContextAsync();
    var authentication = new Authentication { User = Principal("audit-before-display") };
    var session = new CurrentSessionAdapter(authentication, new HttpContextAccessor());
    var options = new DbContextOptionsBuilder<ApplicationDbContext>()
      .UseNpgsql(read.Database.GetConnectionString()).AddInterceptors(new AuditSaveChangesInterceptor(session)).Options;
    await using var db = new ApplicationDbContext(options);
    var entity = new Subdivision { Name = "Session audit synthetic", Path = "/999999/", CreatedBy = "client" };
    try {
      db.Add(entity);
      await db.SaveChangesAsync();
      var saved = await read.Subdivisions.AsNoTracking().SingleAsync(s => s.Id == entity.Id);
      check(saved.CreatedBy == "audit-before-display" && saved.ModifiedBy == null,
        "PostgreSQL legacy audit persists server actor before any display load");
      var createdAt = saved.InsertedDate;
      authentication.User = Principal("audit-next-session");
      entity.Name = "Session audit changed";
      entity.CreatedBy = "forged-client";
      await db.SaveChangesAsync();
      saved = await read.Subdivisions.AsNoTracking().SingleAsync(s => s.Id == entity.Id);
      check(saved.CreatedBy == "audit-before-display" && saved.InsertedDate == createdAt && saved.ModifiedBy == "audit-next-session",
        "PostgreSQL update uses fresh actor and protects stored creator");
      check(saved.LastModifiedDate?.Kind == DateTimeKind.Utc && saved.Status == Guard.Core.Enums.Status.Modified,
        "PostgreSQL audit preserves UTC and modified status");
    }
    finally { await read.Subdivisions.Where(s => s.Id == entity.Id).ExecuteDeleteAsync(); }
  }

  private static ClaimsPrincipal Principal(string id) => new(new ClaimsIdentity([
    new Claim(ClaimTypes.NameIdentifier, id), new Claim("AspNet.Identity.SecurityStamp", "stamp-" + id),
    new Claim("Permission", "Roles.Manage"), new Claim(ClaimTypes.Role, "Root")], "test"));

  private sealed class Authentication : AuthenticationStateProvider
  {
    public ClaimsPrincipal User { get; set; } = new();
    public Exception? Failure { get; set; }
    public Task<AuthenticationState>? Pending { get; set; }
    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Failure != null
      ? Task.FromException<AuthenticationState>(Failure) : Pending ?? Task.FromResult(new AuthenticationState(User));
  }

  // Упражняет реальный pipeline EF и аудит, но не открывает подключение и не подтверждает запись в PostgreSQL.
  private sealed class SuppressSave : SaveChangesInterceptor
  {
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
      => InterceptionResult<int>.SuppressWithResult(0);
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
      InterceptionResult<int> result, CancellationToken cancellationToken = default)
      => ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
  }
}
