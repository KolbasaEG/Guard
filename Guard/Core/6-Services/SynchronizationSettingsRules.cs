using Guard.Core.Entities;

namespace Guard.Core.Services;

internal static class SynchronizationSettingsRules
{
  internal static bool SourceChanged(SynchronizationSettings settings, string? apiBaseUrl, string? apiUsername) =>
    apiBaseUrl != null && !string.Equals(settings.ApiBaseUrl, apiBaseUrl.Trim().TrimEnd('/'), StringComparison.Ordinal) ||
    apiUsername != null && !string.Equals(settings.ApiUsername, apiUsername.Trim(), StringComparison.Ordinal);

  internal static void RequireSnapshotConfirmation(SynchronizationSettings settings)
  {
    if (!settings.CompleteSnapshotGuaranteed)
      throw new EntityRuleException("Подтвердите полноту и согласованность выгрузки API на вкладке «Настройки».");
  }
}
