namespace Guard.Components.Account;

public static class AccountReturnUrl
{
  public static string Normalize(string? value)
  {
    if (string.IsNullOrWhiteSpace(value) || value.Contains('\\') || value.Any(char.IsControl) ||
      !Uri.IsWellFormedUriString(value, UriKind.Relative)) return "/";
    if (value.StartsWith("~/", StringComparison.Ordinal)) value = value[1..];
    if (value.StartsWith("//", StringComparison.Ordinal)) return "/";
    return value.StartsWith('/') ? value : "/" + value;
  }
}
