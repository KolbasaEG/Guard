using Guard.Core.Identity;
using System.Net;

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
foreach (var invalid in new[] { "", "localhost", "127.1", "0x7f000001", "256.1.1.1", "01.2.3.4", "1.2.3.4/33", "::/129", "::/-1", "::/1/2", "fe80::1%3", "1.2.3.4/" })
  Check(!IpAddressRule.TryParse(invalid, out _), $"reject {invalid}");
foreach (var (input, expected) in new[] { (" 192.168.1.9 ", "192.168.1.9"), ("192.168.1.9/24", "192.168.1.0/24"), ("2001:0DB8::1", "2001:db8::1"), ("2001:db8::abcd/64", "2001:db8::/64"), ("1.2.3.4/32", "1.2.3.4"), ("::1/128", "::1"), ("::ffff:192.168.1.1", "192.168.1.1") })
  Check(IpAddressRule.TryParse(input, out var rule) && rule!.CanonicalAddress == expected, $"normalize {input}");
foreach (var (network, client, expected) in new[] { ("192.168.1.128/25", "192.168.1.127", false), ("192.168.1.128/25", "192.168.1.128", true), ("192.168.1.128/25", "192.168.1.255", true), ("192.168.1.128/25", "192.168.2.0", false), ("0.0.0.0/0", "255.255.255.255", true), ("::/0", "2001:db8::1", true), ("::/0", "1.2.3.4", false), ("2001:db8::/64", "2001:db9::1", false), ("192.168.1.1", "::ffff:192.168.1.1", true) })
{
  Check(IpAddressRule.TryParse(network, out var rule) && rule!.Contains(IPAddress.Parse(client)) == expected, $"match {network} against {client}");
}
Check(!IpAddressRule.TryParse("::ffff:192.168.1.1/95", out _), "reject mapped prefix below /96");
Check(IpAddressRule.TryParse("::ffff:192.168.1.123/120", out var mapped) &&
    mapped!.CanonicalAddress == "192.168.1.0/24", "mapped CIDR normalization");
Check(!IpAddressRule.TryParse("[::1]", out _), "bracketed IPv6 is not a catalog address");
foreach (var status in new[] { Guard.Core.Enums.Status.Deleted, Guard.Core.Enums.Status.Blocked, Guard.Core.Enums.Status.ArchivedBlocked })
{
  try { IpStatusTransitions.Apply(status, "archive"); throw new Exception("invalid transition accepted"); }
  catch (InvalidOperationException) { checks++; }
}
Check(IpStatusTransitions.Apply(Guard.Core.Enums.Status.Archived, "block") == Guard.Core.Enums.Status.ArchivedBlocked, "archive block");
Check(IpStatusTransitions.Apply(Guard.Core.Enums.Status.Deleted, "restore") == Guard.Core.Enums.Status.Modified, "restore deleted");
try
{
await AccessEventTests.RunAsync(Check);
AccountPolicyTests.Run(Check);
await LoginLockoutTests.RunAsync(Check);
EntityStatusTests.Run(Check);
IpSearchFilterTests.Run(Check);
await IpSearchUiTests.RunAsync(Check);
await SubdivisionSearchTests.RunAsync(Check);
await ClassifierUiTests.RunAsync(Check);
await UserSearchUiTests.RunAsync(Check);
await AdministrationSearchUiTests.RunAsync(Check);
await UserDialogLifecycleTests.RunAsync(Check);
await RefactorTests.RunAsync(Check);
await ExportTests.RunAsync(Check);
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
Console.WriteLine($"Passed {checks} total checks.");
}
catch (Exception error)
{
  Console.Error.WriteLine(error);
  Environment.ExitCode = 1;
}
