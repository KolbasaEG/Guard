using System.Reflection;
using Guard.Core.Entities;
using Guard.Core.Services;
using Microsoft.AspNetCore.Http;

internal static class ApplicationContractBoundaryTests
{
  public static void Run(Action<bool, string> check)
  {
    var application = typeof(ICurrentSession).Assembly;
    var host = typeof(Guard.GuardServiceRegistration).Assembly;
    var infrastructure = typeof(Guard.Infrastructure.InfrastructureServiceRegistration).Assembly;
    check(application.GetName().Name == "Guard.Application" && application != host && application != typeof(Personal).Assembly,
      "Application has its own assembly, separate from Domain and Web");
    foreach (var reference in application.GetReferencedAssemblies())
      check(reference.Name is "Guard.Domain" or "mscorlib" or "netstandard" or "System" ||
        reference.Name?.StartsWith("System.", StringComparison.Ordinal) == true,
        $"Application depends only on Domain and BCL: {reference.Name}");
    var infrastructureContracts = infrastructure.GetExportedTypes().Where(t => t.IsInterface &&
      (t.Namespace == "Guard.Core.Services" || t.Namespace == "Guard.Core.Services.Exports")).ToHashSet();
    check(infrastructureContracts.Count == 0, "Infrastructure owns no legacy application service contracts");
    foreach (var name in new[] { "IApplicationRoleService", "ILogService", "IClassifierService", "IPersonalService", "ISubdivisionService", "IIpAddressService", "ISecurityService" })
      check(infrastructure.GetType($"Guard.Core.Services.{name}") == null,
        $"Unused legacy contract {name} is removed");
    foreach (var (implementation, contract) in new[] {
      (typeof(ApplicationRoleService), typeof(IRoleSearchService)), (typeof(LogService), typeof(ILogSearchService)) })
      check(implementation.GetInterfaces().SequenceEqual(new[] { contract }) &&
        implementation.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
          .Select(m => m.Name).SequenceEqual(new[] { "SearchAsync" }),
        $"{implementation.Name} exposes only DTO search, without legacy entity/query methods");
    check(typeof(ClassifierService).GetInterfaces().SequenceEqual(new[] { typeof(IClassifierManagementService) }) &&
      typeof(ClassifierService).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Select(m => m.Name).ToHashSet().SetEquals(typeof(IClassifierManagementService).GetMethods().Select(m => m.Name)),
      "ClassifierService exposes only DTO management, without legacy entity/query methods");
    var personalContracts = new[] { typeof(IPersonalSearchService), typeof(IPersonalDetailsService),
      typeof(IPersonalEditorService), typeof(IPersonalStatusService) };
    check(typeof(PersonalService).GetInterfaces().ToHashSet().SetEquals(personalContracts) &&
      typeof(PersonalService).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .ToHashSet().SetEquals(personalContracts.SelectMany(c => typeof(PersonalService).GetInterfaceMap(c).TargetMethods)),
      "PersonalService exposes exactly the narrow DTO and status contracts, without entity/query methods");
    check(typeof(SubdivisionService).GetInterfaces().SequenceEqual(new[] { typeof(ISubdivisionManagementService) }) &&
      typeof(SubdivisionService).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .ToHashSet().SetEquals(typeof(SubdivisionService).GetInterfaceMap(typeof(ISubdivisionManagementService)).TargetMethods),
      "SubdivisionService exposes exactly DTO management and tree operations, without legacy entity/query methods");
    check(typeof(IpAddressService).GetInterfaces().SequenceEqual(new[] { typeof(IIpAddressManagementService) }) &&
      typeof(IpAddressService).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .ToHashSet().SetEquals(typeof(IpAddressService).GetInterfaceMap(typeof(IIpAddressManagementService)).TargetMethods),
      "IpAddressService exposes exactly DTO management, without legacy entity/query methods");
    check(!typeof(SecurityService).GetMethods().Any(m => m.Name == "QueryIpAddressesAsync") &&
      !typeof(SecurityService).GetConstructors().SelectMany(c => c.GetParameters()).Any(p =>
        p.ParameterType == typeof(IpAddressService) || p.ParameterType == typeof(IIpAddressManagementService)),
      "SecurityService has no unused IP query proxy or catalog service dependency");
    foreach (var name in new[] { "QueryUsersAsync", "QueryRolesAsync", "QueryPersonalsAsync", "GetUserRolesAsync",
      "GetRoleClaimsAsync", "UpdateRoleClaimsAsync", "GetUserClaimsAsync", "UpdateUserClaimsAsync",
      "UpdatePersonalIpAddressesAsync", "IsIpAllowedForUserAsync" })
      check(!typeof(SecurityService).GetMethods().Any(m => m.Name == name),
        $"SecurityService removes unused legacy operation {name} from contract and implementation");
    check(typeof(SecurityService).GetConstructors().Single().GetParameters().Select(p => p.ParameterType)
      .SequenceEqual(new[] { typeof(IReadRepository<ApplicationUser>), typeof(IPermissionService),
        typeof(IDataAccessScopeService), typeof(Microsoft.Extensions.DependencyInjection.IServiceScopeFactory),
        typeof(IAuditService), typeof(IAccessChangeNotifier), typeof(IAccountPolicyService),
        typeof(Microsoft.Extensions.Logging.ILogger<SecurityService>) }),
      "SecurityService depends only on user reads, access, operation scopes, delivery and account policy");
    var securityContracts = new[] { typeof(IUserAdministrationService), typeof(IUserListService) };
    check(typeof(SecurityService).GetInterfaces().ToHashSet().SetEquals(securityContracts) &&
      typeof(SecurityService).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .ToHashSet().SetEquals(securityContracts.SelectMany(c => typeof(SecurityService).GetInterfaceMap(c).TargetMethods)),
      "SecurityService exposes exactly the narrow user DTO and list contracts");
    check(!host.GetExportedTypes().Any(t => t.IsInterface && t.Namespace?.StartsWith("Guard.Core.Services", StringComparison.Ordinal) == true),
      "Web owns no application or legacy service interfaces");
    foreach (var type in application.GetExportedTypes())
      check(host.GetType(type.FullName!) == null && infrastructure.GetType(type.FullName!) == null,
        $"Application owns {type.Name} without duplicate Web or Infrastructure definition");
    foreach (var type in new[] { typeof(IpAccessService), typeof(PermissionService), typeof(DataAccessScopeService),
      typeof(IpManagementAccessService), typeof(Guard.Core.Identity.EntityStatusTransitions), typeof(Guard.Core.Identity.IpStatusTransitions),
      typeof(Guard.Core.Identity.AccountPolicyRules), typeof(Guard.Core.Identity.AccountPolicyUserState),
      typeof(ApiSnapshotValidator), typeof(ApiDiagnosticAnalyzer), typeof(SynchronizationSchedule) })
      check(type.Assembly == application, $"Independent access decisions {type.Name} belong to Application");
    var contracts = application.GetExportedTypes()
      .Where(t => t.IsInterface && (t.Namespace == "Guard.Core.Services" ||
        t.Namespace == "Guard.Core.Services.Exports")).ToArray();
    check(contracts.Length == 35, "Application inventory contains 32 services and three access read ports");
    foreach (var name in new[] { "EntityInputValidation", "EntityWriteMapping" })
    {
      var fullName = $"Guard.Core.Services.{name}";
      check(application.GetType(fullName) is { IsNotPublic: true } &&
        infrastructure.GetType(fullName) == null && host.GetType(fullName) == null,
        $"{name} belongs to Application and stays internal");
    }
    var friends = application.GetCustomAttributes<System.Runtime.CompilerServices.InternalsVisibleToAttribute>()
      .Select(a => a.AssemblyName).ToHashSet(StringComparer.Ordinal);
    check(friends.SetEquals(new[] { "Guard.Infrastructure", "Guard.UnitTests", "Guard.IntegrationTests" }),
      "Application internal helpers are shared only with Infrastructure and both test runners");
    foreach (var contract in contracts)
    {
      var violations = FindViolations(contract);
      check(violations.Count == 0,
        $"Application boundary {contract.Name}: {string.Join(", ", violations)}");
    }
    foreach (var dto in application.GetExportedTypes().Where(t => t.Namespace is
      "Guard.Core.Services.DTOs" or "Guard.Core.Services.Exports" && !t.IsInterface))
      check(FindViolations(dto).Count == 0, $"Application DTO boundary {dto.Name}");

    // Ensure the guard follows indirect signatures, rather than only inspecting direct return types.
    foreach (var fixture in new[] { typeof(INestedQuery), typeof(IEntityMethod),
      typeof(IHttpCallback), typeof(IInheritedQuery), typeof(IArrayEntity) })
      check(FindViolations(fixture).Count > 0, $"Boundary guard detects {fixture.Name}");
    check(FindViolations(typeof(ICallback)).Count == 0,
      "Transport-independent callbacks are valid Application contracts");
  }

  private static HashSet<string> FindViolations(Type root)
  {
    var seen = new HashSet<Type>();
    var violations = new HashSet<string>();
    Inspect(root);
    return violations;

    void Inspect(Type type)
    {
      if (!seen.Add(type)) return;
      if (type.HasElementType)
      {
        Inspect(type.GetElementType()!);
        return;
      }
      foreach (var argument in type.GetGenericArguments()) Inspect(argument);
      var ns = type.Namespace ?? "";
      // Only these two independent enums retain the historical Entities namespace.
      var synchronizationEnum = type == typeof(DataManagementMode) || type == typeof(SynchronizationStatus);
      var system = ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal);
      var application = ns == "Guard.Core.Services" || ns.StartsWith("Guard.Core.Services.", StringComparison.Ordinal)
        || ns == "Guard.Core.Enums" || synchronizationEnum;
      var fixture = type.Assembly == typeof(ApplicationContractBoundaryTests).Assembly;
      if (typeof(IQueryable).IsAssignableFrom(type) || (!system && !application && !fixture))
      {
        violations.Add(type.FullName ?? type.Name);
        return;
      }
      if (typeof(Delegate).IsAssignableFrom(type))
      {
        var invoke = type.GetMethod("Invoke");
        if (invoke != null) InspectMethod(invoke);
        return;
      }
      if (system || type.IsEnum) return;
      if (type.BaseType is { } parent && parent != typeof(object)) Inspect(parent);
      foreach (var inherited in type.GetInterfaces()) Inspect(inherited);
      const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
      foreach (var method in type.GetMethods(flags)) InspectMethod(method);
      foreach (var constructor in type.GetConstructors(flags))
        foreach (var parameter in constructor.GetParameters()) Inspect(parameter.ParameterType);
      foreach (var property in type.GetProperties(flags)) Inspect(property.PropertyType);
      foreach (var field in type.GetFields(flags)) Inspect(field.FieldType);
      foreach (var eventInfo in type.GetEvents(flags)) Inspect(eventInfo.EventHandlerType!);
    }

    void InspectMethod(MethodInfo method)
    {
      Inspect(method.ReturnType);
      foreach (var parameter in method.GetParameters()) Inspect(parameter.ParameterType);
    }
  }

  private interface INestedQuery { Task<IReadOnlyList<IQueryable<string>>> ReadAsync(); }
  private interface IEntityMethod { Model Read(); }
  private sealed class Model { public void Apply(Personal entity) { } }
  private interface IHttpCallback { void Subscribe(Func<HttpContext, Task> callback); }
  private interface IInheritedQuery : INestedQuery { }
  private interface IArrayEntity { Personal[] Read(); }
  private interface ICallback { void Subscribe(Func<CancellationToken, Task> callback); }
}
