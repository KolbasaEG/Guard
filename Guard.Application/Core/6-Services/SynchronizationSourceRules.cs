namespace Guard.Core.Services;

public static class SynchronizationSourceRules
{
  public static bool SourceChanged(string currentBaseUrl, string currentUsername, string? apiBaseUrl, string? apiUsername) =>
    apiBaseUrl != null && !string.Equals(currentBaseUrl, apiBaseUrl.Trim().TrimEnd('/'), StringComparison.Ordinal) ||
    apiUsername != null && !string.Equals(currentUsername, apiUsername.Trim(), StringComparison.Ordinal);
}
