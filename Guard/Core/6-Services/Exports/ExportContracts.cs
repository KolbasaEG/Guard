using System.ComponentModel;
using System.Text.Json;
using Guard.Core.Enums;
using Guard.Core.Services.DTOs;

namespace Guard.Core.Services.Exports;

public sealed record ExportRequest(ExportArea Area, ExportFormat Format, DataViewMode Mode = DataViewMode.Active,
  bool IncludeChildren = false, bool AllSubdivisions = false, string? OrderBy = null,
  ExportFilter? Filter = null, PersonalFilter? PersonalFilter = null, string TimeZone = "UTC");
public sealed record ExportFilter(string? Field = null, string? Operation = null, JsonElement? Value = null,
  bool All = true, IReadOnlyList<ExportFilter>? Children = null);
public sealed record ExportJobDto(Guid Id, ExportJobState State, long RowCount, string? Error, DateTimeOffset CreatedAtUtc);
public sealed record ExportArchiveItem(Guid Id, string Author, string? ClientIp, ExportArea Area, ExportFormat Format,
  ExportJobState State, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc, long RowCount, long? Size, string? Sha256);
public sealed record ExportArchivePage(IReadOnlyList<ExportArchiveItem> Items, int Count);
public sealed record ExportArchiveRequest(int Skip = 0, int Take = 20, string? OrderBy = null, ExportFilter? Filter = null);
public sealed record ExportArchiveDetails(string Conditions, string? Sha256, long? Size, DateTimeOffset? DeliveryStartedAtUtc, DateTimeOffset? DeliveryCompletedAtUtc, bool? DeliverySucceeded);
public sealed record ExportRetentionDto(int Years, Guid Version);
public sealed record ExportPreview(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows, bool HasMore);
public sealed record ExportRow(Guid? SubdivisionId, IReadOnlyList<string> Ancestors, IReadOnlyList<ExportCell> Cells);
public sealed record ExportCell(string Text, double? Number = null, bool? Boolean = null);
public sealed record ExportDocumentInfo(Guid Id, string Title, string Author, DateTimeOffset CreatedAtUtc,
  string TimeZone, string Conditions, int SchemaVersion = 1);

public sealed record PersonalExportDto(
  [property: DisplayName("Фамилия")] string LastName,
  [property: DisplayName("Имя")] string FirstName,
  [property: DisplayName("Отчество")] string? MiddleName,
  [property: DisplayName("Подразделение")] string? Subdivision,
  [property: DisplayName("Статус")] string Status);
public sealed record SubdivisionExportDto(
  [property: DisplayName("Наименование")] string Name,
  [property: DisplayName("Родительское подразделение")] string? Parent,
  [property: DisplayName("Тип органа")] string? OrganType,
  [property: DisplayName("Статус")] string Status,
  [property: DisplayName("Наименование для должности")] string? PositionFormationName,
  [property: DisplayName("Почтовый индекс")] string? PostalCode,
  [property: DisplayName("Адрес")] string? Address,
  [property: DisplayName("Телефон")] string? Phone,
  [property: DisplayName("Штатная численность")] double StaffCount,
  [property: DisplayName("Порядок")] int LevelOrder,
  [property: DisplayName("Отдел")] bool IsDepartment);
public sealed record IpAddressExportDto(
  [property: DisplayName("IP-адрес или подсеть")] string Address,
  [property: DisplayName("Описание")] string? Description,
  [property: DisplayName("Подразделение")] string? Subdivision,
  [property: DisplayName("Статус")] string Status);

public sealed class ExportOptions
{
  public string Directory { get; set; } = "App_Data/Exports";
  public int RowsPerPart { get; set; } = 50000;
  public int MaxQueuedPerUser { get; set; } = 3;
  public int TimeoutMinutes { get; set; } = 30;
  public long MinimumFreeBytes { get; set; } = 256L * 1024 * 1024;
}

public interface IDataExportService
{
  Task<Guid> CreateAsync(ExportRequest request, CancellationToken ct = default);
  Task<ExportJobDto> GetAsync(Guid id, CancellationToken ct = default);
  Task<IReadOnlyList<ExportJobDto>> GetActiveAsync(ExportArea area, CancellationToken ct = default);
  Task CancelAsync(Guid id, CancellationToken ct = default);
  Task<ExportArchivePage> SearchArchiveAsync(ExportArchiveRequest request, CancellationToken ct = default);
  Task<ExportPreview> PreviewAsync(Guid id, int part = 1, int skip = 0, CancellationToken ct = default);
  Task<ExportArchiveDetails> GetArchiveDetailsAsync(Guid id, CancellationToken ct = default);
  Task<ExportRetentionDto> GetRetentionAsync(CancellationToken ct = default);
  Task SaveRetentionAsync(int years, Guid version, CancellationToken ct = default);
}
