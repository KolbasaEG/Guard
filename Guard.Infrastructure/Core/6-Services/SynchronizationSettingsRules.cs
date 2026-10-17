using Guard.Core.Entities;

namespace Guard.Core.Services;

internal static class SynchronizationSettingsRules
{
  internal static bool SourceChanged(SynchronizationSettings settings, string? apiBaseUrl, string? apiUsername) =>
    SourceChanged(settings.ApiBaseUrl, settings.ApiUsername, apiBaseUrl, apiUsername);

  internal static bool SourceChanged(string currentBaseUrl, string currentUsername, string? apiBaseUrl, string? apiUsername) =>
    SynchronizationSourceRules.SourceChanged(currentBaseUrl, currentUsername, apiBaseUrl, apiUsername);

  internal static void RequireSnapshotConfirmation(SynchronizationSettings settings)
  {
    if (!settings.CompleteSnapshotGuaranteed)
      throw new EntityRuleException("Подтвердите полноту и согласованность выгрузки API на вкладке «Настройки».");
  }
}
