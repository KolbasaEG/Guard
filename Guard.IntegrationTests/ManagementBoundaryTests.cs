using System.Reflection;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

internal static class ManagementBoundaryTests
{
  public static void Run(Action<bool, string> check)
  {
    var contracts = new[] { typeof(IIpAddressManagementService), typeof(ISubdivisionManagementService),
      typeof(IClassifierManagementService), typeof(IUserListService) };
    var implementations = new[] { typeof(IpAddressService), typeof(SubdivisionService), typeof(ClassifierService), typeof(SecurityService) };
    var registrations = new ServiceCollection();
    registrations.Scan(scan => scan.FromAssemblyOf<IpAddressService>()
      .AddClasses(classes => classes.Where(type => implementations.Contains(type)))
      .AsImplementedInterfaces().WithScopedLifetime());
    foreach (var contract in contracts)
    {
      check(registrations.Count(d => d.ServiceType == contract && d.Lifetime == ServiceLifetime.Scoped) == 1,
        $"Scrutor registers one scoped implementation of {contract.Name}");
      var seen = new HashSet<Type>();
      foreach (var method in contract.GetMethods())
      {
        Inspect(method.ReturnType);
        foreach (var parameter in method.GetParameters()) Inspect(parameter.ParameterType);
      }
      void Inspect(Type type)
      {
        if (!seen.Add(type)) return;
        check(!typeof(IQueryable).IsAssignableFrom(type) && !typeof(Delegate).IsAssignableFrom(type)
          && !(type.Namespace ?? "").StartsWith("Guard.Core.Entities", StringComparison.Ordinal)
          && !(type.Namespace ?? "").StartsWith("Microsoft.AspNetCore.Identity", StringComparison.Ordinal)
          && !(type.Namespace ?? "").StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal),
          $"{contract.Name} does not expose persistence or Identity type {type.Name}");
        foreach (var argument in type.GetGenericArguments()) Inspect(argument);
        if (type.Namespace == "Guard.Core.Services.DTOs")
          foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public)) Inspect(property.PropertyType);
      }
    }
    foreach (var page in new[] { typeof(Guard.Components.Pages.Users.Index), typeof(Guard.Components.Pages.IpAddresses.Index),
      typeof(Guard.Components.Pages.IpAddresses.Add), typeof(Guard.Components.Pages.IpAddresses.Edit),
      typeof(Guard.Components.Pages.Classifiers.Index), typeof(Guard.Components.Pages.Classifiers.Add), typeof(Guard.Components.Pages.Classifiers.Edit),
      typeof(Guard.Components.Pages.Subdivisions.Index), typeof(Guard.Components.Pages.Subdivisions.Add),
      typeof(Guard.Components.Pages.Subdivisions.Edit), typeof(Guard.Components.Pages.Subdivisions.Move) })
    {
      var injected = page.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        .Where(p => p.IsDefined(typeof(InjectAttribute))).Select(p => p.PropertyType).ToArray();
      check(injected.Any(contracts.Contains) && !injected.Intersect(new[] { typeof(ISecurityService)
        }).Any(), $"{page.FullName} consumes narrow management contract");
    }
  }
}
