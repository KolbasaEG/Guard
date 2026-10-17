using System.Reflection;
using Guard.Core.Entities;
using Guard.Core.Services;
using Microsoft.AspNetCore.Http;

internal static class ApplicationContractBoundaryTests
{
  public static void Run(Action<bool, string> check)
  {
    // These transitional contracts belong to Infrastructure, never Application.
    var legacy = new HashSet<Type> { typeof(IPersonalService), typeof(IIpAddressService),
      typeof(ISubdivisionService), typeof(IClassifierService), typeof(ISecurityService),
      typeof(IApplicationRoleService), typeof(ILogService) };
    var contracts = typeof(ICurrentSession).Assembly.GetExportedTypes()
      .Where(t => t.IsInterface && (t.Namespace == "Guard.Core.Services" ||
        t.Namespace == "Guard.Core.Services.Exports") && !legacy.Contains(t)).ToArray();
    check(contracts.Length == 35, "Application inventory contains 32 services and three access read ports");
    foreach (var contract in contracts)
    {
      var violations = FindViolations(contract);
      check(violations.Count == 0,
        $"Application boundary {contract.Name}: {string.Join(", ", violations)}");
    }

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
