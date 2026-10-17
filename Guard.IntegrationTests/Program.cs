using Guard.Core.Identity;

var checks = 0;
void Check(bool condition, string scenario)
{
  if (!condition) throw new Exception($"FAILED: {scenario}");
  checks++;
}
if (args.Length == 2 && args[0] == "--benchmark") {
  try { await PostgresTests.RunAsync(args[1], Check, benchmark: true); }
  catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
  return;
}
foreach (var status in new[] { Guard.Core.Enums.Status.Deleted, Guard.Core.Enums.Status.Blocked, Guard.Core.Enums.Status.ArchivedBlocked })
{
  try { IpStatusTransitions.Apply(status, "archive"); throw new Exception("invalid transition accepted"); }
  catch (InvalidOperationException) { checks++; }
}
Check(IpStatusTransitions.Apply(Guard.Core.Enums.Status.Archived, "block") == Guard.Core.Enums.Status.ArchivedBlocked, "archive block");
Check(IpStatusTransitions.Apply(Guard.Core.Enums.Status.Deleted, "restore") == Guard.Core.Enums.Status.Modified, "restore deleted");
try
{
TestProjectBoundaryTests.Run(Check);
await AccessEventTests.RunAsync(Check);
await CurrentSessionTests.RunAsync(Check);
DomainAssemblyBoundaryTests.Run(Check);
InfrastructureAssemblyBoundaryTests.Run(Check);
AccountPolicyTests.Run(Check);
await AccountPolicyBoundaryTests.RunAsync(Check);
await LoginLockoutTests.RunAsync(Check);
EntityStatusTests.Run(Check);
EntityWriteMappingTests.Run(Check);
IpSearchFilterTests.Run(Check);
SearchFilterContractTests.Run(Check);
await IpSearchUiTests.RunAsync(Check);
await SubdivisionSearchTests.RunAsync(Check);
await ClassifierUiTests.RunAsync(Check);
await UserSearchUiTests.RunAsync(Check);
await AdministrationSearchUiTests.RunAsync(Check);
await UserDialogLifecycleTests.RunAsync(Check);
await RefactorTests.RunAsync(Check);
await ServiceRegistrationTests.RunAsync(Check);
await LogConfigurationTests.RunAsync(Check);
await OwnPasswordBoundaryTests.RunAsync(Check);
await OwnPasswordCompletionTests.RunAsync(Check);
await OwnPasswordUiCompletionTests.RunAsync(Check);
await LocalAccountBoundaryTests.RunAsync(Check);
ManagementBoundaryTests.Run(Check);
ApplicationContractBoundaryTests.Run(Check);
await DialogInfoRenderingTests.RunAsync(Check);
await ExportTests.RunAsync(Check);
await ExportBoundaryTests.RunAsync(Check);
await ApiSynchronizationTests.RunAsync(Check);
await ApiDependencyHttpTests.RunAsync(Check);
ApiDiagnosticTests.Run(Check);
ApiHeadSubdivisionTests.Run(Check);
await ApiDiagnosticTests.CheckAccessAsync(Check);
await SynchronizationUiTests.RunAsync(Check);
await SynchronizationRecoveryTests.RunAsync(Check);
await SynchronizationListLoadTests.RunAsync(Check);
await PersonalPagesTests.RunAsync(Check);
await SubdivisionDialogsTests.RunAsync(Check);
await SubdivisionDialogLifecycleTests.RunAsync(Check);
await IpAddressReferenceTests.RunAsync(Check);
await AccessRenderingTests.RunAsync(Check);
await NavigationTests.RunAsync(Check);
await StaticAssetsTests.RunAsync(Check);
await ThemeBackgroundTests.RunAsync(Check);
await MiddlewareTests.RunAsync(Check);
if (args.Length == 2 && args[0] == "--postgres") await PostgresTests.RunAsync(args[1], Check);
if (args.Length == 2 && args[0] == "--browser") await PostgresTests.RunAsync(args[1], Check, browser: true);
Console.WriteLine($"Passed {checks} integration checks.");
}
catch (Exception error)
{
  Console.Error.WriteLine(error);
  Environment.ExitCode = 1;
}
