using System.Globalization;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using Guard.Core.Services;
using Guard.Core.Entities;
using Microsoft.Extensions.Options;

namespace Guard.Core.Logging;

public sealed class AuditSyslogOptions
{
  public string Host { get; set; } = "localhost";
  public int Port { get; set; } = 6514;
  public string? CaCertificatePath { get; set; }
  public string? CaCertificatePem { get; set; }
}

public interface IAuditTransport
{
  Task SendAsync(AuditEventRecord entry, CancellationToken ct);
}

public sealed class SyslogAuditTransport(IOptions<AuditSyslogOptions> options, AuditJournal? journal = null,
  AuditHealth? health = null) : IAuditTransport
{
  public async Task SendAsync(AuditEventRecord entry, CancellationToken ct)
  {
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
    timeout.CancelAfter(TimeSpan.FromSeconds(15));
    var settings = options.Value;
    if (journal != null) {
      var configuration = await journal.GetConfigurationAsync(timeout.Token);
      settings = new() { Host = configuration.Host, Port = configuration.Port, CaCertificatePem = configuration.CaCertificatePem };
    }
    using var client = new TcpClient();
    await client.ConnectAsync(settings.Host, settings.Port, timeout.Token);
    await using var tls = new SslStream(client.GetStream());
    var authentication = new SslClientAuthenticationOptions {
      TargetHost = settings.Host,
      CertificateRevocationCheckMode = X509RevocationMode.Online
    };
    if (!string.IsNullOrWhiteSpace(settings.CaCertificatePath) || !string.IsNullOrWhiteSpace(settings.CaCertificatePem)) {
      using var ca = settings.CaCertificatePem != null ? X509Certificate2.CreateFromPem(settings.CaCertificatePem)
        : X509CertificateLoader.LoadCertificateFromFile(settings.CaCertificatePath!);
      authentication.CertificateChainPolicy = new X509ChainPolicy {
        TrustMode = X509ChainTrustMode.CustomRootTrust, RevocationMode = X509RevocationMode.Online,
        VerificationFlags = X509VerificationFlags.NoFlag
      };
      authentication.CertificateChainPolicy.CustomTrustStore.Add(ca);
      authentication.CertificateChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.1"));
      await tls.AuthenticateAsClientAsync(authentication, timeout.Token);
    }
    else await tls.AuthenticateAsClientAsync(authentication, timeout.Token);
    if (tls.RemoteCertificate != null) {
      using var remote = new X509Certificate2(tls.RemoteCertificate);
      health?.Certificate(remote.NotAfter.ToUniversalTime());
    }
    var timestamp = entry.OccurredAtUtc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
    var cef = CefFormatter.Format(new AuditEventData(entry.EventId, entry.OccurredAtUtc, entry.EventType,
      entry.Actor, entry.ActorKind, entry.Outcome, entry.ClientIp, entry.CorrelationId,
      entry.ObjectType, entry.ObjectId, entry.Details, entry.ReasonCode, entry.AttemptedAccount));
    var message = Encoding.UTF8.GetBytes($"<134>1 {timestamp} - Guard - Audit - {cef}");
    var prefix = Encoding.ASCII.GetBytes(message.Length.ToString(CultureInfo.InvariantCulture) + " ");
    await tls.WriteAsync(prefix, timeout.Token);
    await tls.WriteAsync(message, timeout.Token);
    await tls.FlushAsync(timeout.Token);
    await tls.ShutdownAsync().WaitAsync(timeout.Token);
    client.Client.Shutdown(SocketShutdown.Send);
  }
}
