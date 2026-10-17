using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services;

internal static class DomainAssemblyBoundaryTests
{
  public static void Run(Action<bool, string> check)
  {
    var domain = typeof(Personal).Assembly;
    var host = typeof(ICurrentSession).Assembly;
    check(domain.GetName().Name == "Guard.Domain" && domain != host,
      "Domain is a separate assembly from Web and application implementations");
    Type[] expected = [typeof(BaseEntity), typeof(IHasSubdivision), typeof(HasFolderEntity), typeof(HasArchiveBatchIdEntity),
      typeof(Personal), typeof(Subdivision), typeof(IpAddress), typeof(Classifier), typeof(AccountPolicy),
      typeof(SynchronizationSettings), typeof(SynchronizationRun), typeof(ApiPersonnelExit),
      typeof(ExportJob), typeof(ExportArtifact), typeof(ExportDeliveryAttempt), typeof(ExportRetentionSettings),
      typeof(Status), typeof(StatusOperation), typeof(DataViewMode), typeof(SubdivisionHierarchyMode),
      typeof(MaintenanceTaskStatus), typeof(ClassifierType), typeof(AuditEventType), typeof(DataManagementMode),
      typeof(SynchronizationStatus), typeof(ExportArea), typeof(ExportFormat), typeof(ExportJobState)];
    check(domain.GetExportedTypes().ToHashSet().SetEquals(expected), "Domain inventory contains the agreed 28 model and enum types");
    foreach (var type in expected)
      check(type.Assembly == domain && type.Namespace is "Guard.Core.Entities" or "Guard.Core.Enums" &&
        host.GetType(type.FullName!) == null, $"Domain owns {type.Name} without duplicate Web definition");
    foreach (var type in new[] { typeof(ApplicationUser), typeof(ApplicationRole), typeof(LogEntry) })
      check(type.Assembly == host, $"Infrastructure model {type.Name} stays outside Domain");
    foreach (var reference in domain.GetReferencedAssemblies())
      check(reference.Name is "mscorlib" or "netstandard" or "System" ||
        reference.Name?.StartsWith("System.", StringComparison.Ordinal) == true,
        $"Domain depends only on BCL: {reference.Name}");
  }
}
