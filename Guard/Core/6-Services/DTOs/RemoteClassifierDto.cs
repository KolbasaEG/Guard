using System.Text.Json.Serialization;

namespace Guard.Core.Services.DTOs
{
  #region Request DTOs

  public class ClassifierApiRequest
  {
    [JsonPropertyName("Data")]
    public ClassifierFilterData Data { get; set; } = new();
  }

  public class ClassifierFilterData
  {
    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;

    [JsonPropertyName("per_page")]
    public int PerPage { get; set; } = 50;

    [JsonPropertyName("ClassifierType")]
    public int? ClassifierType { get; set; }

    [JsonPropertyName("ClassifierCode")]
    public int? ClassifierCode { get; set; }

    [JsonPropertyName("UpdatedAt")]
    public string? UpdatedAt { get; set; }
  }

  #endregion

  #region Response DTOs

  public class ClassifierApiResponse
  {
    [JsonPropertyName("Data")]
    public List<RemoteClassifierDto>? Data { get; set; }

    [JsonPropertyName("Pagination")]
    public PaginationInfoDto? Pagination { get; set; }

    [JsonPropertyName("Success")]
    public bool Success { get; set; }
  }

  public class RemoteClassifierDto
  {
    [JsonPropertyName("ClassifierName")]
    public string ClassifierName { get; set; } = string.Empty;

    [JsonPropertyName("Code")]
    public int Code { get; set; }

    [JsonPropertyName("Type")]
    public int Type { get; set; }

    [JsonPropertyName("Value")]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Поле IsActive в ответе передается как число (1 - активен, 0 - неактивен)
    /// </summary>
    [JsonPropertyName("IsActive")]
    public int IsActive { get; set; }

    [JsonPropertyName("UpdatedAt")]
    public DateTime UpdatedAt { get; set; }

    public bool IsActiveBool => IsActive == 1;
  }

  public class PaginationInfoDto
  {
    [JsonPropertyName("Page")]
    public int Page { get; set; }

    [JsonPropertyName("PerPage")]
    public int PerPage { get; set; }

    [JsonPropertyName("TotalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("TotalPages")]
    public int TotalPages { get; set; }
  }

  #endregion
}
