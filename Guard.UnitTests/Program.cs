var checks = 0;
void Check(bool condition, string scenario)
{
  if (!condition) throw new Exception($"FAILED: {scenario}");
  checks++;
}

try
{
  if (args.Length != 0) throw new ArgumentException("Unit checks do not accept database, browser or benchmark options.");
  IpAddressRuleTests.Run(Check);
  EntityStatusTests.Run(Check);
  IpStatusTests.Run(Check);
  AccountPolicyRuleTests.Run(Check);
  EntityWriteMappingTests.Run(Check);
  EntityInputValidationTests.Run(Check);
  ApiSnapshotValidationTests.Run(Check);
  ApiDiagnosticAnalyzerTests.Run(Check);
  ApiDiagnosticRuleTests.Run(Check);
  SynchronizationScheduleTests.Run(Check);
  NotificationRuleTests.Run(Check);
  await IpAccessBoundaryTests.RunAsync(Check);
  await PermissionBoundaryTests.RunAsync(Check);
  await DataAccessScopeBoundaryTests.RunAsync(Check);
  await IpManagementAccessBoundaryTests.RunAsync(Check);
  Console.WriteLine($"Passed {checks} unit checks.");
}
catch (Exception error)
{
  Console.Error.WriteLine(error);
  Environment.ExitCode = 1;
}
