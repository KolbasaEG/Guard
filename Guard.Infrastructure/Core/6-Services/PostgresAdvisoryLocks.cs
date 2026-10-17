namespace Guard.Core.Services;

internal static class PostgresAdvisoryLocks
{
  // Синхронизация и диагностика API исключают друг друга во всех экземплярах Guard.
  internal const long ApiSynchronization = 73460220;

  // Очередь экспорта имеет собственного владельца, независимого от синхронизации.
  internal const long ExportQueue = 73460221;
}
