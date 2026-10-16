using Guard.Core.Services.Exports;

namespace Guard.Core.Services.DTOs;

public sealed record ClassifierDetailsDto
{
  public int Id { get; init; }
  public Guid Version { get; init; }
  public int Type { get; set; }
  public int Code { get; set; }
  public string ClassifierName { get; set; } = "";
  public string Value { get; set; } = "";
  public bool IsActive { get; set; } = true;
  public DateTime UpdatedAt { get; init; }

  public ClassifierFieldsDto ToFields() => new() {
    Type = Type, Code = Code, ClassifierName = ClassifierName, Value = Value, IsActive = IsActive
  };
}

public sealed record ClassifierSearchRequest(int Skip = 0, int Take = 20, string? OrderBy = null, ExportFilter? Filter = null);
public sealed record ClassifierSearchResult(IReadOnlyList<ClassifierListDto> Items, int Count);
