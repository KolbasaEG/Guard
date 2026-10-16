using Guard.Core.Enums;
using Guard.Core.Services.Exports;

namespace Guard.Core.Services.DTOs;

public sealed record SubdivisionSearchRequest(
  DataViewMode Mode = DataViewMode.Active,
  SubdivisionHierarchyMode Hierarchy = SubdivisionHierarchyMode.CurrentOnly,
  bool AllSubdivisions = false, int Skip = 0, int Take = 20,
  string? OrderBy = null, ExportFilter? Filter = null);

public sealed record SubdivisionSearchResult(IReadOnlyList<SubdivisionListDto> Items, int Count);
