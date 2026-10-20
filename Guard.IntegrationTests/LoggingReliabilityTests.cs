using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Logging;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Guard.Core.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

internal static class LoggingReliabilityTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    {
      var text = CefFormatter.Format(AuditEventData.Create(AuditEventType.LoginFailed, "user=x\\y\r\nother", "127.0.0.1", "{Injected} msg=bad\nnext"));
      check(text.Contains("{Injected}") && text.Contains("externalId="), "CEF retains data braces and persistent event identity");
      check(!text.Contains('\r') && !text.Contains('\n') && text.Contains("user\\=x\\\\y\\r\\nother") && text.Contains("msg\\=bad\\nnext"),
        "CEF escapes backslashes, equals and line boundaries without creating another event");
      check(text.Contains("outcome=failure") && CefFormatter.Format(AuditEventType.ExportCancelled, null, null, null).Contains("outcome=cancelled"),
        "audit distinguishes denied and cancelled outcomes");
    }
    using (var unconfigured = new AuditService(new LoggerConfiguration().CreateLogger()))
      await PostgresTests.ThrowsAsync<AuditUnavailableException>(() => unconfigured.RecordAsync(AuditEventData.Create(AuditEventType.LoginSuccess, "test")), check,
        "a logger without a mandatory journal cannot claim successful audit storage");
    var root = Path.Combine(Path.GetTempPath(), "guard-logging-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try {
      var previousDirectory = Environment.CurrentDirectory;
      try {
        Environment.CurrentDirectory = root;
        var capture = new AuditCapture();
        using var manager = new DynamicLoggerManager(new LoggingLevelSwitch(), (_, _) => new LoggerConfiguration().CreateLogger());
        manager.ApplyConfiguration(100, LogEventLevel.Information);
        var service = new LogConfigurationService(new Access(), manager, NullLogger<LogConfigurationService>.Instance, capture);
        var result = await service.SaveAsync(new(231, ApplicationLogLevel.Fatal));
        check(result.MinimumLevel == ApplicationLogLevel.Fatal && capture.Events.Count == 2 && capture.Events.All(entry =>
          entry!.Contains("Information->Fatal") && entry.Contains("100->231")),
          "raising technical threshold to Fatal retains independent audit with actual old and new settings");
        capture.Fail = true;
        await PostgresTests.ThrowsAsync<IOException>(() => service.SaveAsync(new(232, ApplicationLogLevel.Error)), check,
          "failed mandatory intent prevents a logging configuration change");
        check(manager.GetConfiguration() == (231, LogEventLevel.Fatal),
          "failed audit preserves the persisted and active logging configuration");
      }
      finally { Environment.CurrentDirectory = previousDirectory; }

      var notDirectory = Path.Combine(root, "existing-file");
      await File.WriteAllTextAsync(notDirectory, "sentinel");
      var originalError = Console.Error;
      using var output = new StringWriter();
      try {
        Console.SetError(output);
        new InternalLogWriter(notDirectory).Write("test-independent-diagnostics");
      }
      finally { Console.SetError(originalError); }
      check(output.ToString().Contains("test-independent-diagnostics") && await File.ReadAllTextAsync(notDirectory) == "sentinel",
        "unavailable diagnostic directory preserves stderr and does not damage an existing file");

      var rotating = Path.Combine(root, "rotation"); Directory.CreateDirectory(rotating);
      var active = Path.Combine(rotating, $"serilog-internal-errors-{DateTime.UtcNow:yyyyMMdd}.log");
      using (var file = File.Create(active)) file.SetLength(10 * 1024 * 1024);
      new InternalLogWriter(rotating).Write("after-rotation");
      check(Directory.GetFiles(rotating).Length == 2 && new FileInfo(active).Length < 1024,
        "SelfLog rotates at size limit and preserves the prior segment");

      await CheckTlsAsync(root, check);
    }
    finally { Directory.Delete(root, recursive: true); }

    var parallel = await Task.WhenAll(Enumerable.Range(1, 8).Select(async n => {
      using var scope = AuditOperationContext.Push(new("actor-" + n, "192.0.2." + n, "operation-" + n, "user"));
      await Task.Yield();
      var entry = AuditEventData.Create(AuditEventType.EntityChanged, null);
      return entry.Actor == "actor-" + n && entry.ClientIp == "192.0.2." + n && entry.CorrelationId == "operation-" + n;
    }));
    foreach (var correct in parallel) check(correct, "parallel operation keeps its own audit actor, IP and correlation");
    check(AuditOperationContext.Current == null, "operation metadata does not leak to its caller");
  }

  private static async Task CheckTlsAsync(string directory, Action<bool, string> check)
  {
    using var rsa = RSA.Create(2048);
    var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    var names = new SubjectAlternativeNameBuilder(); names.AddDnsName("localhost");
    request.CertificateExtensions.Add(names.Build());
    request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
    request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, false));
    using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
    using var certificate = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
    using var expiredGenerated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(-1));
    using var expiredCertificate = X509CertificateLoader.LoadPkcs12(expiredGenerated.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
    foreach (var mode in new[] { "untrusted", "wrong-name", "expired", "trusted" }) {
      var serverCertificate = mode == "expired" ? expiredCertificate : certificate;
      var caPath = Path.Combine(directory, $"test-ca-{mode}.cer");
      await File.WriteAllBytesAsync(caPath, serverCertificate.Export(X509ContentType.Cert));
      using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(15));
      using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
      var port = ((IPEndPoint)listener.LocalEndpoint).Port;
      var received = "";
      var server = Task.Run(async () => {
        using var client = await listener.AcceptTcpClientAsync(stopping.Token);
        await using var ssl = new SslStream(client.GetStream());
        try {
          await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = serverCertificate }, stopping.Token);
          var prefix = new List<byte>(); var one = new byte[1];
          while (await ssl.ReadAsync(one, stopping.Token) == 1 && one[0] != (byte)' ') {
            prefix.Add(one[0]); if (prefix.Count > 10) throw new Exception("Invalid RFC6587 frame");
          }
          if (prefix.Count == 0) return;
          var size = int.Parse(Encoding.ASCII.GetString(prefix.ToArray()));
          var payload = new byte[size]; await ssl.ReadExactlyAsync(payload, stopping.Token);
          received = Encoding.UTF8.GetString(payload);
        }
        catch (Exception error) when (mode != "trusted" && error is AuthenticationException or IOException) { }
      }, stopping.Token);
      var transport = new SyslogAuditTransport(Options.Create(new AuditSyslogOptions {
        Host = mode == "wrong-name" ? "127.0.0.1" : "localhost", Port = port,
        CaCertificatePath = mode == "untrusted" ? null : caPath
      }));
      var entry = new AuditEventRecord { EventId = Guid.NewGuid(), EventType = AuditEventType.LoginSuccess,
        OccurredAtUtc = DateTime.UtcNow, Actor = "тест", Details = "message\nnew=field" };
      if (mode == "trusted") {
        await transport.SendAsync(entry, stopping.Token); await server;
        check(received.StartsWith("<134>1 ") && received.Contains("externalId=" + entry.EventId) &&
          received.Contains("тест") && !received.Contains('\n') && received.Contains("new\\=field"),
          "trusted TLS receives one correctly byte-framed RFC5424/CEF event including immutable ID and UTF8");
      }
      else {
        await PostgresTests.ThrowsAsync<AuthenticationException>(() => transport.SendAsync(entry, stopping.Token), check,
          $"TLS rejects {mode} certificate");
        await server;
        check(received.Length == 0, "invalid TLS certificate receives no audit payload");
      }
    }
  }

  private sealed class Sink : ILogEventSink { public List<LogEvent> Events = []; public void Emit(LogEvent item) => Events.Add(item); }
  private sealed class AuditCapture : IAuditService
  {
    public List<string?> Events = []; public bool Fail;
    public Task RecordAsync(Guard.Core.Services.AuditEventData entry, CancellationToken ct = default) { ct.ThrowIfCancellationRequested(); LogIdentityEvent(entry.EventType, entry.Actor, entry.ClientIp, entry.Details); return Task.CompletedTask; }
    public void LogIdentityEvent(AuditEventType type, string? actor, string? ip = null, string? details = null)
    { if (Fail) throw new IOException(); Events.Add(details); }
  }
  private sealed class Access : IPermissionService
  {
    public Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) => Task.FromResult(new UserAccessSnapshot("root", true, []));
    public Task<UserAccessSnapshot> GetForUserAsync(string id, CancellationToken ct = default) => GetCurrentAsync(ct);
    public Task<bool> HasAsync(string code, CancellationToken ct = default) => Task.FromResult(true);
    public Task RequireAsync(string code, CancellationToken ct = default) => Task.CompletedTask;
  }
}
