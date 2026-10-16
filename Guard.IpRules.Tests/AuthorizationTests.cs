using Guard.Core.Services.Exports;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Repositories;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using System.Text;

internal static class AuthorizationTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory, Action<bool,string> check)
  {
    var authentication = new Authentication(factory) { Id = "root" };
    var services = new ServiceCollection();
    services.AddLogging(); services.AddHttpContextAccessor();
    services.AddSingleton(factory); services.AddScoped(_ => factory.CreateDbContext());
    services.AddSingleton<AuthenticationStateProvider>(authentication);
    services.AddScoped<ICurrentSession, Guard.Components.Account.CurrentSessionAdapter>();
    services.AddSingleton<IAuditService,AuditStub>();
    services.AddSingleton<IAccessChangeNotifier,AccessChangeNotifier>();
    services.AddIdentity<ApplicationUser,ApplicationRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders().AddUserManager<PolicyUserManager>();
    services.AddScoped<IPasswordValidator<ApplicationUser>,PolicyPasswordValidator>();
    services.AddScoped<IPermissionService,PermissionService>();
    services.AddScoped<IAccountPolicyService,AccountPolicyService>();
    services.AddScoped<IPasswordRequirementsService,AccountPolicyService>();
    services.AddScoped<IAccountPolicyAdministrationService,AccountPolicyService>();
    services.AddScoped<IDataAccessScopeService,DataAccessScopeService>();
    services.AddScoped<IRoleAccessService,RoleAccessService>();
    services.AddScoped(typeof(IReadRepository<>),typeof(ReadRepository<>));
    services.AddScoped<IUnitOfWork,UnitOfWork>();
    services.AddScoped<IUnitOfWorkFactory,UnitOfWorkFactory>();
    services.AddScoped(typeof(IGenericRepository<>),typeof(Guard.Core.Repositories.GenericRepository<>));
    services.AddScoped<IPersonalService,PersonalService>();
    services.AddScoped(typeof(IBasicRepository<>),typeof(BasicRepository<>));
    services.AddScoped<ISubdivisionService,SubdivisionService>();
    services.AddScoped<IClassifierService,ClassifierService>();
    services.AddScoped<IIpManagementAccessService,IpManagementAccessService>();
    services.AddScoped<IIpAddressService,IpAddressService>();
    services.AddScoped<IPersonalIpService,PersonalIpService>();
    services.AddScoped<IIpAccessService,IpAccessService>();
    services.AddScoped<ISecurityService,SecurityService>();
    await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes=true, ValidateOnBuild=true });
    await using var scope = provider.CreateAsyncScope();
    var roles=scope.ServiceProvider.GetRequiredService<IRoleAccessService>();
    var notifications = 0;
    var committedAssignment = false;
    using var subscription = provider.GetRequiredService<IAccessChangeNotifier>().Subscribe("user", async () => {
      notifications++;
      await using var committed = await factory.CreateDbContextAsync();
      committedAssignment = await committed.UserRoles.AnyAsync(r => r.UserId == "user");
    });
    var permissions=scope.ServiceProvider.GetRequiredService<IPermissionService>();
    var scopes=scope.ServiceProvider.GetRequiredService<IDataAccessScopeService>();
    var personals=scope.ServiceProvider.GetRequiredService<IPersonalService>();
    var reads=scope.ServiceProvider.GetRequiredService<IReadRepository<Personal>>();
    var usersReads=scope.ServiceProvider.GetRequiredService<IReadRepository<ApplicationUser>>();
    await using(var db = await factory.CreateDbContextAsync()) {
      var own=await db.Subdivisions.SingleAsync(s=>s.Name=="Own");
      var child=new Subdivision { Name="Child", Path=own.Path+"3/", ParentId=own.Id, CreatedBy="test" };
      db.AddRange(child,new Personal {FirstName="Child",LastName="Person",Subdivision=child,PersonalNumber="SECRET",CreatedBy="test"});
      db.UserClaims.Add(new(){UserId="user",ClaimType="Permission",ClaimValue=Permissions.Roles.Manage});
      db.Users.Add(new ApplicationUser { Id="unassigned",UserName="unassigned",NormalizedUserName="UNASSIGNED" });
      await db.SaveChangesAsync();
    }
    var displayService = new CurrentUserService(permissions, scopes, factory);
    var rootDisplay = (await displayService.GetDisplayAsync())!;
    check(rootDisplay.IsRoot && rootDisplay.UserId == "root", "display DTO identifies Root without an Identity graph");
    check(rootDisplay.SubordinateSubdivisions.Count >= 3, "Root display selector includes all active subdivisions");
    check(((ICollection<CurrentUserSubdivisionDto>)rootDisplay.SubordinateSubdivisions).IsReadOnly, "display subdivision snapshot is read-only");
    var changingScope = new CurrentUserService(permissions, new ChangingDisplayScope(scopes), factory);
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => changingScope.GetDisplayAsync(), check,
        "display result is rejected when server scope changes during loading");
    var forgedDisplay = rootDisplay with { UserId = "forged", IsRoot = false };
    check((await scope.ServiceProvider.GetRequiredService<ICurrentSession>().GetAsync())?.UserId == "root" && forgedDisplay.UserId == "forged", "UI DTO replacement cannot change session identity");
    authentication.Id = "user";
    var userDisplay = (await displayService.GetDisplayAsync())!;
    var userScope = await scopes.GetAsync();
    check(!userDisplay.IsRoot && userDisplay.SubordinateSubdivisions.Count == 2 &&
        userDisplay.SubordinateSubdivisions.All(s => userScope.Allows(s.Id)), "ordinary display selector is restricted to own subdivision and descendants");
    check((await scope.ServiceProvider.GetRequiredService<ICurrentSession>().GetAsync())?.UserId == "user", "session identity follows server authentication without a display cache");
    authentication.Id = "unassigned";
    var unassignedDisplay = (await displayService.GetDisplayAsync())!;
    check(unassignedDisplay.SubdivisionName == null && unassignedDisplay.PersonalFullName == null &&
        unassignedDisplay.SubordinateSubdivisions.Count == 0, "unassigned account display has no subdivision options");
    using (var cancelled = new CancellationTokenSource()) {
      cancelled.Cancel();
      await PostgresTests.ThrowsAsync<OperationCanceledException>(() => displayService.GetDisplayAsync(cancelled.Token), check, "display query honors cancellation");
    }
    authentication.Id = "root";
    check((await permissions.GetCurrentAsync()).IsRoot,"Root recognized from current database role");
    check((await permissions.GetCurrentAsync()).Has(Permissions.Personals.Export),"Root grants catalog rights");
    check(!(await permissions.GetCurrentAsync()).Has("invented"),"Root does not grant unknown permission");
    await PostgresTests.ThrowsAsync<ArgumentException>(()=>roles.SaveAsync(new(){Name="Bad",Permissions=["invented"]}),check,"unknown role permission rejected");
    await PostgresTests.ThrowsAsync<ArgumentException>(()=>roles.SaveAsync(new(){Name="Bad",Permissions=[Permissions.Personals.Write]}),check,"missing dependency rejected");
    await PostgresTests.ThrowsAsync<ArgumentException>(()=>roles.SaveAsync(new(){Name="Bad",Permissions=[Permissions.Roles.Manage]}),check,"Root-only permission rejected");
    await PostgresTests.ThrowsAsync<InvalidOperationException>(()=>roles.DeleteAsync("root-role"),check,"Root role deletion rejected");
    var rootRole=await roles.GetAsync("root-role");
    await PostgresTests.ThrowsAsync<InvalidOperationException>(()=>roles.SaveAsync(rootRole),check,"Root role changes rejected");
    var rootAssignments=await roles.GetUserRolesAsync("root");
    await PostgresTests.ThrowsAsync<InvalidOperationException>(()=>roles.SetUserRolesAsync("root",[],rootAssignments.Version),check,"last Root cannot be removed");
    var viewerId=await roles.SaveAsync(new(){Name="Viewer",Permissions=[Permissions.Personals.Read]});
    var exporterId=await roles.SaveAsync(new(){Name="Exporter",Permissions=[Permissions.Personals.Read,Permissions.Personals.Export]});
    await RoleInputSnapshotTests.RunAsync(scope.ServiceProvider, check);
    var legacyRoleId = await roles.SaveAsync(new() { Name = "Legacy synchronization", Permissions = [Permissions.Subdivisions.Read] });
    await using (var db = await factory.CreateDbContextAsync())
    {
      db.RoleClaims.Add(new() { RoleId = legacyRoleId, ClaimType = "Permission", ClaimValue = Permissions.Synchronization.Run });
      await db.SaveChangesAsync();
    }
    var legacyRole = await roles.GetAsync(legacyRoleId);
    check(legacyRole.Permissions.SetEquals([Permissions.Subdivisions.Read]), "role editor omits retired synchronization claim and preserves valid permissions");
    var validVersion = legacyRole.Version;
    legacyRole.Version = Guid.NewGuid().ToString();
    await PostgresTests.ThrowsAsync<InvalidOperationException>(() => roles.SaveAsync(legacyRole), check, "stale edit cannot clean legacy role claims");
    await using (var db = await factory.CreateDbContextAsync())
      check(await db.RoleClaims.AnyAsync(c => c.RoleId == legacyRoleId && c.ClaimValue == Permissions.Synchronization.Run), "failed role save leaves retired claim unchanged");
    legacyRole.Version = validVersion;
    await roles.SaveAsync(legacyRole);
    await using (var db = await factory.CreateDbContextAsync())
    {
      var values = await db.RoleClaims.Where(c => c.RoleId == legacyRoleId && c.ClaimType == "Permission").Select(c => c.ClaimValue).ToListAsync();
      check(values.Count == 1 && values[0] == Permissions.Subdivisions.Read, "successful role save removes retired claim and retains other rights");
    }
    await PostgresTests.ThrowsAsync<ArgumentException>(() => roles.SaveAsync(new() { Name = "Bad legacy", Permissions = [Permissions.Subdivisions.Read, Permissions.Synchronization.Run] }), check, "retired permission cannot be submitted as a new right");
    var userRoles=await roles.GetUserRolesAsync("user");
    await roles.SetUserRolesAsync("user",[viewerId],userRoles.Version);
    await PostgresTests.ThrowsAsync<InvalidOperationException>(()=>roles.SetUserRolesAsync("user",[exporterId],userRoles.Version),check,"stale assignment version rejected");
    check(notifications == 1 && committedAssignment, "assignment publishes after commit; rejected stale update publishes nothing");
    check((await roles.GetTransitionReportAsync()).IndividualPermissionCount==1,"ignored individual permissions reported");
    authentication.Id="user";
    var viewer=await permissions.GetCurrentAsync();
    check(viewer.Has(Permissions.Personals.Read) && !viewer.Has(Permissions.Personals.ReadDetails),"list and details permissions distinct");
    check(!viewer.Has(Permissions.Roles.Manage),"individual permission cannot elevate user");
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => scope.ServiceProvider.GetRequiredService<IIpAddressService>().SearchAsync(new()),
      check, "IP SearchAsync denies callers without role-based read permission");
    var ipService = scope.ServiceProvider.GetRequiredService<IIpAddressService>();
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => ipService.GetDetailsAsync(Guid.NewGuid()), check, "IP details require role-based read permission");
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => ipService.GetSubdivisionOptionsAsync(), check, "IP options require role-based read permission");
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(()=>roles.GetAsync(viewerId),check,"non-Root role management denied");
    var accessScope=await scopes.GetAsync();
    check(accessScope.SubdivisionIds.Count==2,"scope includes own subdivision and descendant");
    var list=await personals.SearchAsync(new(Take:100));
    check(list.Count==2 && list.Items.All(i=>i.LastName!="User" || i.FirstName=="Test"),"list and count exclude foreign subdivision");
    check(typeof(PersonalListItemDto).GetProperty("PersonalNumber")==null,"list DTO excludes sensitive number");
    check((await personals.SearchAsync(new(Search:"Child"))).Count==1,"search uses same authorized scope");
    var foreignId=Guid.Empty;
    await using(var db=await factory.CreateDbContextAsync()) {foreignId=await db.Subdivisions.Where(s=>s.Name=="Other").Select(s=>s.Id).SingleAsync();}
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(()=>personals.SearchAsync(new(SubdivisionId:foreignId)),check,"foreign filter rejected");
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(()=>reads.GetAllAsync(),check,"raw entity read requires full-card permission");
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(()=>new ExportAccess(factory, permissions).ForUserAsync("user", new(Guard.Core.Enums.ExportArea.Personals, Guard.Core.Enums.ExportFormat.Excel), default),check,"export needs explicit permission");
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(()=>personals.CreateAsync(new(){FirstName="X",LastName="Y"}),check,"read-only employee write denied");
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(()=>usersReads.GetAllAsync(),check,"user module read needs permission");
    var handler=new PermissionAuthorizationHandler(permissions, new AuditStub());
    var principal=(await authentication.GetAuthenticationStateAsync()).User;
    var context=new AuthorizationHandlerContext([new PermissionRequirement(Permissions.Personals.Read)],principal,null);
    await handler.HandleAsync(context); check(context.HasSucceeded,"route permission handler allows role permission");
    authentication.Id="unassigned";
    check((await scopes.GetAsync()).SubdivisionIds.Count==0,"no employee binding yields empty scope");
    authentication.Id="root";
    var assignment=await roles.GetUserRolesAsync("user");
    await roles.SetUserRolesAsync("user",[viewerId,exporterId],assignment.Version);
    authentication.Id="user";
    check((await permissions.GetCurrentAsync()).Has(Permissions.Personals.Export),"permissions are union of current assigned roles");
    var exportAccess = await new ExportAccess(factory, permissions).ForUserAsync("user", new(Guard.Core.Enums.ExportArea.Personals, Guard.Core.Enums.ExportFormat.Excel), default);
    check(exportAccess.AllowedIds.Count == 2 && !ExportSchema.Headers(Guard.Core.Enums.ExportArea.Personals).Contains("Личный номер"), "export respects scope and safe fields");
    authentication.Id="root";
    var editor=await roles.GetAsync(viewerId);
    var oldVersion=editor.Version;
    editor.Permissions.Add(Permissions.Personals.ReadDetails);
    await roles.SaveAsync(editor);
    check(notifications == 3, "role permission update notifies assigned user");
    editor.Version=oldVersion;
    await PostgresTests.ThrowsAsync<InvalidOperationException>(()=>roles.SaveAsync(editor),check,"stale role version rejected");
    authentication.Id="user";
    check((await reads.GetAllAsync()).Count==2,"full-card entity access remains scoped");
    var card=await personals.GetDetailsAsync(list.Items[0].Id);
    check(card!=null && typeof(PersonalDetailsDto).GetProperty("PersonalNumber") != null && typeof(PersonalDetailsDto).GetProperty("PasswordHash") == null,"full-card DTO includes employee fields without Identity secrets");
    authentication.Id="root";
    var revoke=await roles.GetAsync(viewerId); revoke.Permissions.Clear(); await roles.SaveAsync(revoke);
    await roles.DeleteAsync(exporterId);
    check(notifications == 5, "permission revocation and role deletion notify former members");
    authentication.Id="user";
    check(!(await permissions.GetCurrentAsync()).Has(Permissions.Personals.Read),"revocation takes effect without new login");
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(()=>personals.SearchAsync(new()),check,"next read denied after revocation");
    context=new AuthorizationHandlerContext([new PermissionRequirement(Permissions.Personals.Read)],principal,null);
    await handler.HandleAsync(context); check(!context.HasSucceeded,"route handler denies stale principal after revocation");
    authentication.Id="root";
    var concurrentRole = await roles.GetAsync(viewerId);
    var saves = await Task.WhenAll(Enumerable.Range(0,2).Select(async i => {
      var input = new RoleEditDto { Id=concurrentRole.Id, Name="Viewer"+i, Version=concurrentRole.Version, Permissions=[Permissions.Personals.Read] };
      try { await roles.SaveAsync(input); return true; } catch(InvalidOperationException) { return false; }
    }));
    check(saves.Count(v=>v)==1,"concurrent role saves have exactly one winner");
    var rollbackRoles = await roles.GetUserRolesAsync("user");
    await PostgresTests.ThrowsAsync<ArgumentException>(()=>roles.SetUserRolesAsync("user",["unknown"],rollbackRoles.Version),check,"unknown role assignment rejected");
    check((await roles.GetUserRolesAsync("user")).Selected.SequenceEqual(rollbackRoles.Selected),"rejected assignment preserves existing roles");
    var final=await roles.GetUserRolesAsync("user");
    await roles.SetUserRolesAsync("user",["admin-role"],final.Version);
    var security=scope.ServiceProvider.GetRequiredService<ISecurityService>();
    await using(var availableDb = await factory.CreateDbContextAsync()) {
      availableDb.Personals.Add(new Personal { FirstName = "Available Other", LastName = "Test", SubdivisionId = foreignId, CreatedBy = "test" });
      await availableDb.SaveChangesAsync();
    }
    check((await security.GetUserPersonalOptionsAsync()).Any(p => p.Name.Contains("Other")), "Root sees employees from all subdivisions in user creation selector");
    await PostgresTests.ThrowsAsync<InvalidOperationException>(()=>security.CreateUserAsync(
      new(){UserName="rollback-user",Email="rollback@example.test"},"TestPassword123!",["unknown"]),check,"user creation with invalid role fails");
    await using(var db=await factory.CreateDbContextAsync()) check(!await db.Users.AnyAsync(u=>u.UserName=="rollback-user"),"failed compound user creation rolls back");
    authentication.Id="user";
    check(await usersReads.CountAsync()==1,"user count excludes foreign and unbound accounts");
    var subdivisions=scope.ServiceProvider.GetRequiredService<ISubdivisionService>();
    check((await subdivisions.GetAllActiveAsync()).Count==2,"subdivision reads include own descendants only");
    await PostgresTests.ThrowsAsync<KeyNotFoundException>(()=>subdivisions.UpdateAsync(new(){Id=foreignId,Name="Forbidden"}),check,"foreign subdivision update rejected");
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(()=>((IUserListService)security).SetUserLockoutAsync("root",true),check,"ordinary manager cannot lock Root");
    await PostgresTests.ThrowsAsync<InvalidOperationException>(async ()=>await security.UpdateUserAsync(
      (await security.GetUserByIdAsync("user"))!,["Root"]),check,"profile editing cannot change roles");
    authentication.Id="root";
    await using(var db=await factory.CreateDbContextAsync()) {
      await db.RoleClaims.Where(c=>c.RoleId=="admin-role").ExecuteDeleteAsync();
      check(await db.UserClaims.CountAsync(c=>c.UserId=="user" && c.ClaimType=="Permission")==1,"individual claims remain in database");
    }
    await roles.InitializeAdministratorAsync();
    check((await roles.GetAsync("admin-role")).Permissions.Count==PermissionCatalog.All.Count(p=>!p.RootOnly),"explicit initialization sets complete non-system permissions");
    await PostgresTests.ThrowsAsync<InvalidOperationException>(()=>roles.InitializeAdministratorAsync(),check,"initialization does not overwrite configured role");
    authentication.Id="user";
    var foreignPersonalId=Guid.Empty;
    await using(var db=await factory.CreateDbContextAsync()) foreignPersonalId=await db.Personals.Where(p=>p.FirstName=="Other").Select(p=>p.Id).SingleAsync();
    check(await personals.GetByIdAsync(foreignPersonalId)==null,"foreign full-card lookup returns no record");
    var options = await security.GetUserPersonalOptionsAsync();
    check(options.Count == 1 && options.All(p => p.Id != foreignPersonalId), "user creation selector exposes only active unbound employees in allowed subdivisions");
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => security.CreateUserAsync(
      new() { UserName = "outside", Email = "outside@example.test", PersonalId = foreignPersonalId }, "TestPassword123!"), check, "creating user for foreign employee is denied");
    var createdUser = new ApplicationUser { UserName = "browser-create", Email = "browser-create@example.test", PersonalId = options[0].Id };
    check((await security.CreateUserAsync(createdUser, "TestPassword123!")).Succeeded, "user creation succeeds for available employee");
    await using (var createdDb = await factory.CreateDbContextAsync()) {
      check(await createdDb.Users.AnyAsync(u => u.Id == createdUser.Id && u.PersonalId == options[0].Id), "created user is stored with employee binding");
      check(!await createdDb.UserRoles.AnyAsync(r => r.UserId == createdUser.Id), "new user has no automatically assigned roles");
    }
    await PostgresTests.ThrowsAsync<InvalidOperationException>(() => security.CreateUserAsync(
      new() { UserName = "browser-create", Email = "duplicate@example.test", PersonalId = options[0].Id }, "TestPassword123!"), check, "employee with existing account cannot be assigned twice");
    var anotherPersonalId = Guid.NewGuid();
    await using (var employeeDb = await factory.CreateDbContextAsync()) {
      var subdivisionId = await employeeDb.Personals.Where(p => p.Id == options[0].Id).Select(p => p.SubdivisionId).SingleAsync();
      employeeDb.Personals.Add(new Personal { Id = anotherPersonalId, FirstName = "New", LastName = "Test", SubdivisionId = subdivisionId, CreatedBy = "test" });
      await employeeDb.SaveChangesAsync();
    }
    await PostgresTests.ThrowsAsync<InvalidOperationException>(() => security.CreateUserAsync(
      new() { UserName = "browser-create", Email = "duplicate@example.test", PersonalId = anotherPersonalId }, "TestPassword123!"), check, "Identity rejects duplicate login for a different employee");
    await PostgresTests.ThrowsAsync<InvalidOperationException>(() => security.CreateUserAsync(
      new() { UserName = "weak-password", Email = "weak@example.test", PersonalId = anotherPersonalId }, "x"), check, "weak password is rejected");
    await PostgresTests.ThrowsAsync<KeyNotFoundException>(()=>personals.UpdateAsync(new(){Id=foreignPersonalId,FirstName="X",LastName="Y"}),check,"foreign employee update denied");
    await using(var db=await factory.CreateDbContextAsync()) await db.Subdivisions.Where(s=>s.Name=="Own").ExecuteUpdateAsync(s=>s.SetProperty(p=>p.Path,"/"));
    check((await scopes.GetAsync()).SubdivisionIds.Count==0,"malformed root path fails closed");
    check((await personals.SearchAsync(new())).Count==0,"malformed scope cannot expose all records");
    check((await displayService.GetDisplayAsync())!.SubordinateSubdivisions.Count == 0, "display reload fails closed after subdivision path change");
    await using(var db=await factory.CreateDbContextAsync()) await db.Users.Where(u=>u.Id=="user").ExecuteUpdateAsync(s=>s.SetProperty(u=>u.LockoutEnd,DateTimeOffset.UtcNow.AddDays(1)));
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(()=>permissions.GetCurrentAsync(),check,"locked account loses application permissions on next operation");
    check(await displayService.GetDisplayAsync() == null, "locked account receives no display snapshot");
    authentication.Id="";
    var currentUser = new CurrentUserService(permissions, scopes, factory);
    check(await currentUser.GetDisplayAsync()==null,"anonymous layout safely receives empty display context");
    authentication.Id = "root";
    await AccountPolicyIntegrationTests.RunAsync(scope.ServiceProvider, factory, check);
  }
  private sealed class ChangingDisplayScope(IDataAccessScopeService inner) : IDataAccessScopeService
  {
    private int calls;
    public async Task<DataAccessScope> GetAsync(CancellationToken ct = default)
    {
      var scope = await inner.GetAsync(ct);
      return ++calls == 1 ? scope : scope with { UserId = "different-actor" };
    }
  }
  private sealed class Authentication(IDbContextFactory<ApplicationDbContext> factory) : AuthenticationStateProvider {
    public string Id {get;set;}="";
    public override async Task<AuthenticationState> GetAuthenticationStateAsync() {
      await using var db = await factory.CreateDbContextAsync();
      var stamp = await db.Users.Where(u => u.Id == Id).Select(u => u.SecurityStamp).SingleOrDefaultAsync();
      return new(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,Id),new Claim("AspNet.Identity.SecurityStamp",stamp ?? "")],"test")));
    }
  }
}
