using System.Net;
using System.Net.Sockets;

namespace Guard.Core.Identity;

/// <summary>Разбор и сопоставление отдельных IPv4/IPv6 и CIDR без DNS-запросов.</summary>
public sealed class IpAddressRule
{
  private readonly byte[] _network;
  private readonly int _prefixLength;
  public string CanonicalAddress { get; }

  private IpAddressRule(IPAddress address, int prefixLength, bool subnet)
  {
    _network = address.GetAddressBytes();
    _prefixLength = prefixLength;
    for (var bit = prefixLength; bit < _network.Length * 8; bit++)
      _network[bit / 8] &= (byte)~(1 << (7 - bit % 8));
    CanonicalAddress = new IPAddress(_network).ToString() + (subnet ? $"/{prefixLength}" : "");
  }

  public static bool TryParse(string? value, out IpAddressRule? rule)
  {
    rule = null;
    if (string.IsNullOrWhiteSpace(value)) return false;
    var parts = value.Trim().Split('/');
    if (value.Contains('%') || value.Contains('[') || value.Contains(']')) return false;
    // CIDR mapped IPv6 требует преобразования префикса /96..128 в IPv4 /0..32.
    if (parts.Length == 2 && IPAddress.TryParse(parts[0], out var mapped) && mapped.IsIPv4MappedToIPv6)
    {
      if (!int.TryParse(parts[1], System.Globalization.NumberStyles.None,
          System.Globalization.CultureInfo.InvariantCulture, out var mappedPrefix) || mappedPrefix < 96 || mappedPrefix > 128)
        return false;
      parts[0] = mapped.MapToIPv4().ToString();
      parts[1] = (mappedPrefix - 96).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
    if (parts.Length > 2 || !TryParseClient(parts[0], out var address)) return false;
    var max = address!.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
    var prefix = max;
    if (parts.Length == 2 && (!int.TryParse(parts[1], System.Globalization.NumberStyles.None,
        System.Globalization.CultureInfo.InvariantCulture, out prefix) || prefix < 0 || prefix > max))
      return false;
    rule = new IpAddressRule(address, prefix, parts.Length == 2 && prefix != max);
    return true;
  }

  public static bool TryParseClient(string? value, out IPAddress? address)
  {
    address = null;
    if (string.IsNullOrWhiteSpace(value) || value.Contains('%') || value.Contains('/') ||
        value.Contains('[') || value.Contains(']') || value != value.Trim()) return false;
    // IPAddress.TryParse также принимает сокращённую IPv4-нотацию; каталог требует четыре октета.
    if (!value.Contains(':'))
    {
      var octets = value.Split('.');
      if (octets.Length != 4 || octets.Any(o => o.Length == 0 || o.Length > 3 ||
          o.Any(c => c < '0' || c > '9') || (o.Length > 1 && o[0] == '0'))) return false;
    }
    if (!IPAddress.TryParse(value, out address)) return false;
    if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
    return true;
  }

  public bool Contains(IPAddress address)
  {
    if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
    var bytes = address.GetAddressBytes();
    if (bytes.Length != _network.Length) return false;
    var fullBytes = _prefixLength / 8;
    for (var i = 0; i < fullBytes; i++)
      if (bytes[i] != _network[i]) return false;
    var remaining = _prefixLength % 8;
    return remaining == 0 || (bytes[fullBytes] & (255 << (8 - remaining))) == _network[fullBytes];
  }
}
