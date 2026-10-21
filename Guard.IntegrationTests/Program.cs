var checks = 0;
if (args.Length == 2 && args[0] == "--technical-crash-probe") {
  await TechnicalLoggingReadinessTests.CrashChildAsync(args[1]); return;
}
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
if (args.Length == 2 && args[0] == "--technical-benchmark") {
  try { await PostgresTests.RunAsync(args[1], Check, technicalBenchmark: true); }
  catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
  return;
}
try
{
TestProjectBoundaryTests.Run(Check);
await AccessEventTests.RunAsync(Check);
await CurrentSessionTests.RunAsync(Check);
ClientIpAccessorTests.Run(Check);
await IpCatalogMaintenanceCommandTests.RunAsync(Check);
DomainAssemblyBoundaryTests.Run(Check);
InfrastructureAssemblyBoundaryTests.Run(Check);
AccountPolicyTests.Run(Check);
await AccountPolicyBoundaryTests.RunAsync(Check);
await LoginLockoutTests.RunAsync(Check);
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
await WorkerLifecycleTests.RunAsync(Check);
await LogConfigurationTests.RunAsync(Check);
await LoggingReliabilityTests.RunAsync(Check);
await TechnicalLoggingTests.RunAsync(Check);
await TechnicalLoggingReadinessTests.RunAsync(Check);
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
await AuditCookieHandlerTests.RunAsync(Check);
await AuditColdStartTests.RunAsync(Check);
if (args.Length == 2 && args[0] == "--postgres") await PostgresTests.RunAsync(args[1], Check);
if (args.Length == 2 && args[0] == "--browser") await PostgresTests.RunAsync(args[1], Check, browser: true);
Console.WriteLine($"Passed {checks} integration checks.");
}
catch (Exception error)
{
  Console.Error.WriteLine(error);
  Environment.ExitCode = 1;
}
