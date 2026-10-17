using Guard.Web.Exports;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.Exports;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

internal static partial class ExportIntegrationTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory, Action<bool, string> check)
  {
    var region = new Subdivision { Name = "Export region", CreatedBy = "test" };
    var own = new Subdivision { Name = "Export own", ParentId = region.Id, CreatedBy = "test" };
    var child = new Subdivision { Name = "Export child", ParentId = own.Id, CreatedBy = "test" };
    var foreign = new Subdivision { Name = "Export foreign", ParentId = region.Id, CreatedBy = "test" };
    var personal = new Personal { FirstName = "EXPORT_OWN", LastName = "Test", SubdivisionId = own.Id, CreatedBy = "test" };
    var descendant = new Personal { FirstName = "EXPORT_CHILD", LastName = "Test", SubdivisionId = child.Id, CreatedBy = "test" };
    var outsider = new Personal { FirstName = "EXPORT_FOREIGN", LastName = "Test", SubdivisionId = foreign.Id, CreatedBy = "test" };
    var user = new ApplicationUser { Id = "export-user", UserName = "Export user", SecurityStamp = "export-stamp", PersonalId = personal.Id };
    var root = new ApplicationUser { Id = "export-root", UserName = "Export root", SecurityStamp = "export-root-stamp" };
    var role = new ApplicationRole { Id = "export-role", Name = "Export role", NormalizedName = "EXPORT ROLE" };
    var rootRole = new ApplicationRole { Id = "export-root-role", Name = "Root", NormalizedName = "ROOT" };
    await using (var db = await factory.CreateDbContextAsync()) {
      db.IsSynchronization = true;
      // Reuse the existing Root role if present: normalized names are unique.
      rootRole = await db.Roles.SingleAsync(r => r.NormalizedName == "ROOT");
      db.AddRange(region, own, child, foreign, personal, descendant, outsider, user, root, role);
      db.UserRoles.AddRange(new() { UserId = user.Id, RoleId = role.Id }, new() { UserId = root.Id, RoleId = rootRole.Id });
      foreach (var p in new[] { Permissions.Personals.Read, Permissions.Personals.Export, Permissions.Subdivisions.Read, Permissions.Subdivisions.Export, Permissions.IpAddresses.Read, Permissions.IpAddresses.Export })
        db.RoleClaims.Add(new() { RoleId = role.Id, ClaimType = "Permission", ClaimValue = p });
      db.IpAddresses.AddRange(new() { Address = "198.18.200.1", SubdivisionId = own.Id, CreatedBy = "test" }, new() { Address = "198.18.200.2", SubdivisionId = child.Id, CreatedBy = "test" }, new() { Address = "198.18.200.3", SubdivisionId = foreign.Id, CreatedBy = "test" });
      await db.SaveChangesAsync();
      region.Path = $"/{region.SubdivisionId}/"; own.Path = region.Path + own.SubdivisionId + "/";
      child.Path = own.Path + child.SubdivisionId + "/"; foreign.Path = region.Path + foreign.SubdivisionId + "/";
      await db.SaveChangesAsync();
    }
    var env = new ExportTests.EnvironmentStub(Path.Combine(Path.GetTempPath(), "guard-export-integration-" + Guid.NewGuid().ToString("N")));
    Directory.CreateDirectory(env.ContentRootPath);
    var opts = Options.Create(new ExportOptions { Directory = "archive", MinimumFreeBytes = 0, RowsPerPart = 100 });
    var storage = new ExportStorage(new EphemeralDataProtectionProvider(), opts, env);
    var auth = new TestAuthentication(factory) { UserId = user.Id };
    var http = new HttpContextAccessor();
    await using var provider = new ServiceCollection().AddLogging().BuildServiceProvider();
    var policies = new AccountPolicyService(factory, provider, new AuditStub(), new AccessChangeNotifier(NullLogger<AccessChangeNotifier>.Instance));
    var permissions = new PermissionService(new Guard.Components.Account.CurrentSessionAdapter(auth, http), new PermissionReader(factory), new AuditStub(), policies);
    var access = new ExportAccess(factory, permissions);
    var service = new DataExportService(factory, permissions, access, storage, opts, new AuditStub(), new Guard.Web.Identity.ClientIpAccessor(http), NullLogger<DataExportService>.Instance);
    var processor = new ExportProcessor(factory, access, new ExportSnapshot(factory, storage), new ExportDocumentWriter(storage, opts), storage, new AuditStub(), opts, NullLogger<ExportProcessor>.Instance);
    try {
      await ExportCommandCompletionTests.RunAsync(factory, permissions, access, storage, opts, http,
        id => auth.UserId = id, user.Id, root.Id, () => processor.CleanupAsync(default), check);
      await ExportCompletionTests.RunAsync(factory, service, access, storage, opts, user.Id, check);
      foreach (var area in Enum.GetValues<ExportArea>()) foreach (var format in Enum.GetValues<ExportFormat>()) {
        var id = await service.CreateAsync(new(area, format, IncludeChildren: true));
        await processor.RunNextAsync(default);
        var result = await service.GetAsync(id);
        if (result.State != ExportJobState.Ready) throw new Exception("Export processor failed: " + result.Error);
        check(result.RowCount == 2, "database export includes all authorized descendants and excludes foreign records");
        auth.UserId = root.Id;
        var archivePage = await service.SearchArchiveAsync(new(OrderBy: ""));
        check(archivePage.Items.Any(j => j.Id == id), "archive first load accepts empty sort and includes generated export");
        var preview = await service.PreviewAsync(id);
        check(preview.Rows.Count == 2 && !preview.Rows.Any(r => r.Any(c => c.Contains("EXPORT_FOREIGN"))), "Root preview reads exact archived safe fields");
        if (format == ExportFormat.Excel) check(preview.Rows[0][0] == region.Name && preview.Headers.Take(3).SequenceEqual(new[] { "Уровень 1", "Уровень 2", "Уровень 3" }), "Excel chain starts above actor scope at common root");
        auth.UserId = user.Id;
        await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.PreviewAsync(id), check, "non-Root archive preview denied");
        await service.CancelAsync(id); await processor.CleanupAsync(default);
      }
      await using (var db = await factory.CreateDbContextAsync()) {
        var setting = await db.SynchronizationSettings.SingleOrDefaultAsync(s => s.Id == 1);
        if (setting != null) { setting.Mode = DataManagementMode.Api; await db.SaveChangesAsync(); }
      }
      var apiJob = await service.CreateAsync(new(ExportArea.Personals, ExportFormat.Excel, IncludeChildren: true));
      await processor.RunNextAsync(default);
      check((await service.GetAsync(apiJob)).State == ExportJobState.Ready, "read-only API mode permits authorized export");
      await CheckHttpDeliveryAsync(factory, policies, storage, opts, apiJob, user, check);
      await CheckHttpAuditCompletionAsync(factory, policies, storage, opts, service, processor, user, root, check);
      await CheckDeliveryContractAsync(factory, service, processor,
        new ExportDeliveryService(permissions, access, storage, factory, new AuditStub(), provider.GetRequiredService<ILoggerFactory>()), check);
      var filteredId = await service.CreateAsync(new(ExportArea.Personals, ExportFormat.Word, IncludeChildren: true, PersonalFilter: new("FirstName", "Equals", "EXPORT_CHILD")));
      await processor.RunNextAsync(default); check((await service.GetAsync(filteredId)).RowCount == 1, "applied personal filter narrows complete export");
      auth.UserId = root.Id;
      var archiveFilter = new ExportFilter("Area", "Equals", JsonSerializer.SerializeToElement(ExportArea.IpAddresses));
      var archiveRows = await service.SearchArchiveAsync(new(Take: 100, OrderBy: "Id", Filter: archiveFilter));
      var archiveNext = await service.SearchArchiveAsync(new(Skip: 1, Take: 1, OrderBy: "Id", Filter: archiveFilter));
      check(archiveRows.Count >= 2 && archiveRows.Items.All(j => j.Area == ExportArea.IpAddresses) &&
        archiveNext.Count == archiveRows.Count && archiveNext.Items.Single().Id == archiveRows.Items[1].Id,
        "archive SQL repeats enum-filter validation and counts before stable pagination");
      var target = archiveRows.Items[0];
      ExportFilter Window(DateTimeOffset start, DateTimeOffset end) => new(Children: [
        new("Id", "Equals", JsonSerializer.SerializeToElement(target.Id)), new(Children: [
          new("CreatedAtUtc", "GreaterThanOrEquals", JsonSerializer.SerializeToElement(start.ToOffset(TimeSpan.FromHours(3)))),
          new("CreatedAtUtc", "LessThan", JsonSerializer.SerializeToElement(end.ToOffset(TimeSpan.FromHours(3))))])]);
      var exactWindow = await service.SearchArchiveAsync(new(Filter: Window(target.CreatedAtUtc, target.CreatedAtUtc.AddSeconds(1))));
      var excludedWindow = await service.SearchArchiveAsync(new(Filter: Window(target.CreatedAtUtc.AddSeconds(1), target.CreatedAtUtc.AddSeconds(2))));
      check(exactWindow.Items.Single().Id == target.Id && excludedWindow.Count == 0,
        "archive PostgreSQL applies nested timestamp ranges and normalizes offset values to UTC");
      await PostgresTests.ThrowsAsync<ArgumentException>(() => service.SearchArchiveAsync(new(Filter: new("SecurityStamp", "Equals", JsonSerializer.SerializeToElement("x")))),
        check, "archive service rejects unsupported fields without trusting UI validation");
      await PostgresTests.ThrowsAsync<ArgumentException>(() => service.SearchArchiveAsync(new(Filter: new("CreatedAtUtc", "Equals", JsonSerializer.SerializeToElement("bad-date")))),
        check, "archive service rejects malformed typed values");
      await PostgresTests.ThrowsAsync<ArgumentException>(() => service.SaveRetentionAsync(0, Guid.Empty), check, "retention lower bound checked on server");
      await PostgresTests.ThrowsAsync<ArgumentException>(() => service.SaveRetentionAsync(31, Guid.Empty), check, "retention upper bound checked on server");
      var settingValue = await service.GetRetentionAsync(); await service.SaveRetentionAsync(4, settingValue.Version);
      var previousSetting = settingValue;
      settingValue = await service.GetRetentionAsync();
      check(settingValue.Years == 4 && previousSetting.Version != settingValue.Version, "Root can configure retention and receive a fresh DTO version");
      await PostgresTests.ThrowsAsync<DbUpdateConcurrencyException>(() => service.SaveRetentionAsync(5, Guid.NewGuid()), check, "stale retention version rejected");
      auth.UserId = user.Id;
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.GetRetentionAsync(), check, "non-Root cannot read retention DTO");
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveRetentionAsync(5, settingValue.Version), check, "non-Root cannot save retention");
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.SearchArchiveAsync(new(Filter: archiveFilter)), check, "archive filter does not bypass Root access");
      var future = await service.CreateAsync(new(ExportArea.IpAddresses, ExportFormat.Excel));
      await using (var db = await factory.CreateDbContextAsync()) {
        var job = await db.ExportJobs.SingleAsync(j => j.Id == future);
        check(job.ExpiresAtUtc.Year == job.CreatedAtUtc.AddYears(4).Year, "new retention applies only to new jobs");
        job.ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1); await db.SaveChangesAsync();
      }
      await processor.CleanupAsync(default);
      await using (var db = await factory.CreateDbContextAsync()) {
        var expired = await db.ExportJobs.SingleAsync(j => j.Id == future);
        check(expired.State == ExportJobState.Expired && expired.AuthorId == "" && expired.ClientIp == null, "expired job removes personal registry data and cannot be issued");
      }
      var revoked = await service.CreateAsync(new(ExportArea.Personals, ExportFormat.Word));
      await using (var db = await factory.CreateDbContextAsync()) {
        var claim = await db.RoleClaims.SingleAsync(c => c.RoleId == role.Id && c.ClaimValue == Permissions.Personals.Export); db.RoleClaims.Remove(claim); await db.SaveChangesAsync();
      }
      await processor.RunNextAsync(default); check((await service.GetAsync(revoked)).State == ExportJobState.Failed, "permission revocation before worker blocks generation");
      string expiredKey;
      await using (var db = await factory.CreateDbContextAsync()) {
        var artifact = await db.ExportArtifacts.SingleAsync(a => a.JobId == filteredId);
        expiredKey = artifact.StorageKey;
        var job = await db.ExportJobs.SingleAsync(j => j.Id == filteredId); job.ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1); await db.SaveChangesAsync();
        check(File.Exists(storage.ArtifactPath(artifact.StorageKey)), "archive exists before expiry cleanup");
      }
      await processor.CleanupAsync(default);
      await using (var db = await factory.CreateDbContextAsync()) {
        check(!await db.ExportArtifacts.AnyAsync(a => a.JobId == filteredId) && !File.Exists(storage.ArtifactPath(expiredKey)), "expiry deletes exact copy and artifact metadata");
      }
    }
    finally { Directory.Delete(env.ContentRootPath, true); }
  }
  private static async Task CheckHttpDeliveryAsync(IDbContextFactory<ApplicationDbContext> factory, IAccountPolicyService policies, ExportStorage storage, IOptions<ExportOptions> options, Guid id, ApplicationUser user, Action<bool, string> check)
  {
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" }); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
    builder.Services.AddSingleton(factory); builder.Services.AddSingleton(policies); builder.Services.AddSingleton(storage);
    builder.Services.AddSingleton(options); builder.Services.AddSingleton<IAuditService, AuditStub>(); builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<AuthenticationStateProvider, HttpAuthentication>();
    builder.Services.AddScoped<ICurrentSession, Guard.Components.Account.CurrentSessionAdapter>();
    builder.Services.AddScoped<IPermissionReader, PermissionReader>();
    builder.Services.AddScoped<IPermissionService, PermissionService>(); builder.Services.AddScoped<ExportAccess>(); builder.Services.AddScoped<IExportDeliveryService, ExportDeliveryService>();
    builder.Services.AddAuthentication("export-test").AddScheme<AuthenticationSchemeOptions, TestHandler>("export-test", _ => { });
    builder.Services.AddAuthorization(); builder.Services.AddAntiforgery();
    await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.UseAntiforgery();
    app.MapGet("/csrf", (HttpContext ctx, IAntiforgery anti) => Results.Json(anti.GetAndStoreTokens(ctx)));
    app.MapExportEndpoints(); await app.StartAsync();
    try {
      var url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
      using var handler = new HttpClientHandler { CookieContainer = new CookieContainer() }; using var client = new HttpClient(handler) { BaseAddress = new Uri(url) };
      client.DefaultRequestHeaders.Add("X-Test-User", user.Id);
      var tokens = JsonSerializer.Deserialize<AntiforgeryTokenSet>(await client.GetStringAsync("/csrf"), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
      using var noToken = await client.PostAsync($"/exports/{id}/download", new StringContent("")); check(noToken.StatusCode == HttpStatusCode.Forbidden, "download requires antiforgery token");
      var form = new[] { new KeyValuePair<string, string>(tokens.FormFieldName, tokens.RequestToken!) };
      using var response = await client.PostAsync($"/exports/{id}/download", new FormUrlEncodedContent(form));
      check(response.StatusCode == HttpStatusCode.OK && response.Content.Headers.ContentDisposition?.DispositionType == "attachment", "authorized one-time endpoint delivers file");
      var delivered = await response.Content.ReadAsByteArrayAsync();
      await using (var db = await factory.CreateDbContextAsync()) {
        var artifact = await db.ExportArtifacts.SingleAsync(a => a.JobId == id);
        check(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(delivered)) == artifact.Sha256, "HTTP delivered bytes match archived SHA256");
      }
      using var repeat = await client.PostAsync($"/exports/{id}/download", new FormUrlEncodedContent(form)); check(repeat.StatusCode == HttpStatusCode.Conflict, "repeat download denied atomically");
    }
    finally { await app.StopAsync(); }
  }
  private sealed class TestAuthentication(IDbContextFactory<ApplicationDbContext> factory) : AuthenticationStateProvider
  {
    public string UserId { get; set; } = "";
    public override async Task<AuthenticationState> GetAuthenticationStateAsync() {
      await using var db = await factory.CreateDbContextAsync(); var stamp = await db.Users.Where(u => u.Id == UserId).Select(u => u.SecurityStamp).SingleAsync();
      return new(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserId), new Claim("AspNet.Identity.SecurityStamp", stamp!)], "test")));
    }
  }
  private sealed class HttpAuthentication : AuthenticationStateProvider
  {
    public override Task<AuthenticationState> GetAuthenticationStateAsync() => throw new InvalidOperationException("Use HTTP identity.");
  }
  private sealed class TestHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, IDbContextFactory<ApplicationDbContext> factory)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
  {
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync() {
      var id = Request.Headers["X-Test-User"].ToString(); await using var db = await factory.CreateDbContextAsync();
      var stamp = await db.Users.Where(u => u.Id == id).Select(u => u.SecurityStamp).SingleOrDefaultAsync();
      if (stamp == null) return AuthenticateResult.NoResult();
      return AuthenticateResult.Success(new(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id), new Claim("AspNet.Identity.SecurityStamp", stamp)], Scheme.Name)), Scheme.Name));
    }
  }
}
