using Guard.Core.Enums;
using Guard.Core.Services.Exports;

namespace Guard.Core.Services.DTOs;

// ExportFilter — независимый сериализуемый снимок; формат существующих заданий сохраняется.
public sealed record IpSearchRequest(DataViewMode Mode = DataViewMode.Active,
  SubdivisionHierarchyMode Hierarchy = SubdivisionHierarchyMode.CurrentOnly,
  bool AllSubdivisions = false, int Skip = 0, int Take = 20,
  string? OrderBy = null, ExportFilter? Filter = null);

public sealed record IpSearchResult(IReadOnlyList<IpAddressDto> Items, int Count);
