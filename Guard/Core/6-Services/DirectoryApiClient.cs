using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Guard.Core.Services.DTOs;
using Microsoft.Extensions.Options;

namespace Guard.Core.Services;

public sealed class DirectoryApiOptions
{
  public string BaseUrl { get; set; } = "";
  public string Username { get; set; } = "";
  public string Password { get; set; } = "";
  public int PageSize { get; set; } = 500;
}

public interface IDirectoryApiClient
{
  Task<ApiSnapshot> FetchAsync(CancellationToken ct);
  Task<ApiSnapshot> FetchAsync(string baseUrl, int pageSize, CancellationToken ct, string? username = null, string? password = null, DateOnly? updatedSince = null, long? headSubdivisionId = null, Func<string, CancellationToken, Task>? phaseChanged = null);
  Task<ApiSnapshot> FetchDiagnosticAsync(string baseUrl, int pageSize, string username, string password, DateOnly updatedSince, CancellationToken ct, IProgress<string>? progress = null);
  Task<ApiSnapshot> ResolveDependenciesAsync(ApiSnapshot snapshot, IReadOnlySet<(int Type, int Code)> knownClassifiers,
    IReadOnlySet<long> knownSubdivisions, string baseUrl, string username, string password, CancellationToken ct);
  Task PingAsync(string baseUrl, string username, string password, CancellationToken ct);
}

public sealed class DirectoryApiClient(HttpClient http, IOptions<DirectoryApiOptions> options, ILogger<DirectoryApiClient> logger) : IDirectoryApiClient
{
  private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
  private sealed class Page<T>
  {
    public bool Success { get; set; }
    public List<T>? Data { get; set; }
    public PaginationInfoDto? Pagination { get; set; }
  }

  public async Task<ApiSnapshot> FetchAsync(CancellationToken ct)
    => await FetchAsync(options.Value.BaseUrl, options.Value.PageSize, ct);

  public async Task<ApiSnapshot> FetchAsync(string baseUrl, int pageSize, CancellationToken ct, string? username = null, string? password = null, DateOnly? updatedSince = null, long? headSubdivisionId = null, Func<string, CancellationToken, Task>? phaseChanged = null)
    => await FetchCoreAsync(baseUrl, pageSize, ct, username, password, updatedSince, false, null, headSubdivisionId, phaseChanged);

  public async Task<ApiSnapshot> FetchDiagnosticAsync(string baseUrl, int pageSize, string username, string password, DateOnly updatedSince, CancellationToken ct, IProgress<string>? progress = null)
  {
    if (updatedSince == default) throw new ArgumentException("Укажите дату UpdatedAt для диагностики.");
    return await FetchCoreAsync(baseUrl, pageSize, ct, username, password, updatedSince, true, progress);
  }

  private async Task<ApiSnapshot> FetchCoreAsync(string baseUrl, int pageSize, CancellationToken ct, string? username, string? password, DateOnly? updatedSince, bool diagnostic, IProgress<string>? progress, long? headSubdivisionId = null, Func<string, CancellationToken, Task>? phaseChanged = null)
  {
    var settings = options.Value;
    username ??= settings.Username;
    password ??= settings.Password;
    if (!Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var address) ||
        address.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(username) ||
        string.IsNullOrEmpty(password) || pageSize is < 1 or > 500)
      throw new EntityRuleException("Не настроено защищённое подключение к API.");
    var authorization = Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password));
    progress?.Report("Получение классификаторов");
    if (phaseChanged != null) await phaseChanged("Classifiers", ct);
    var classifiers = await FetchPages<RemoteClassifierDto>(address, "classifiers", pageSize, authorization, ct, updatedSince);
    progress?.Report("Получение подразделений");
    if (phaseChanged != null) await phaseChanged("Subdivisions", ct);
    var subdivisions = await FetchPages<ApiSubdivision>(address, "subdivisions", pageSize, authorization, ct, updatedSince);
    progress?.Report("Получение сотрудников");
    if (phaseChanged != null) await phaseChanged("Personnel", ct);
    var personnel = await FetchPages<ApiPersonal>(address, "personnel", pageSize, authorization, ct, updatedSince);
    if (updatedSince.HasValue && phaseChanged != null) await phaseChanged("ExitEvents", ct);
    if (updatedSince.HasValue) progress?.Report("Получение событий выхода сотрудников");
    var exits = updatedSince.HasValue
      ? await FetchPages<ApiPersonnelExitEvent>(address, "personnel/out-of-scope", pageSize, authorization, ct, updatedSince) : [];
    var snapshot = new ApiSnapshot(classifiers, subdivisions, personnel) { UpdatedSince = updatedSince, HeadSubdivisionId = headSubdivisionId, ExitEvents = exits };
    if (!diagnostic) ApiSnapshotValidator.Validate(snapshot, allowMissingReferences: updatedSince.HasValue);
    return snapshot;
  }

  public async Task PingAsync(string baseUrl, string username, string password, CancellationToken ct)
  {
    var address = ConnectionAddress(baseUrl);
    using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(address, "ping"));
    request.Headers.Authorization = new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password)));
    using var response = await http.SendAsync(request, ct);
    response.EnsureSuccessStatusCode();
    logger.LogInformation("API: проверка ping завершена, HTTP {ApiStatusCode}", (int)response.StatusCode);
  }

  private static Uri ConnectionAddress(string baseUrl)
  {
    if (!Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var address) || address.Scheme != Uri.UriSchemeHttps ||
        address.UserInfo.Length != 0 || address.Query.Length != 0 || address.Fragment.Length != 0)
      throw new EntityRuleException("Укажите HTTPS-адрес API без учётных данных и параметров запроса.");
    return address;
  }

  public async Task<ApiSnapshot> ResolveDependenciesAsync(ApiSnapshot snapshot, IReadOnlySet<(int Type, int Code)> knownClassifiers,
    IReadOnlySet<long> knownSubdivisions, string baseUrl, string username, string password, CancellationToken ct)
  {
    if (!snapshot.UpdatedSince.HasValue) return snapshot;
    var address = ConnectionAddress(baseUrl);
    var authorization = Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password));
    var subdivisions = snapshot.Subdivisions.ToDictionary(s => s.Id);
    var classifiers = snapshot.Classifiers.ToDictionary(c => (c.Type, c.Code));
    var requests = 0;
    void CountRequest()
    {
      if (++requests > 512) throw new EntityRuleException("Превышен предел дозагрузки зависимостей. Выполните полную загрузку или выберите более раннюю дату.");
    }
    while (true)
    {
      var missing = snapshot.Personnel.Select(p => p.SubdivisionId)
        .Concat(subdivisions.Values.Where(s => s.Id != snapshot.HeadSubdivisionId && s.ParentId is > 0).Select(s => s.ParentId!.Value))
        .Where(id => !knownSubdivisions.Contains(id) && !subdivisions.ContainsKey(id)).Distinct().ToArray();
      if (missing.Length == 0) break;
      foreach (var id in missing)
      {
        ct.ThrowIfCancellationRequested(); CountRequest();
        logger.LogInformation("API: дозагрузка подразделения {ExternalRecordId}, запрос {DependencyRequestCount}", id, requests);
        List<ApiSubdivision> items;
        try { items = await FetchPages<ApiSubdivision>(address, "subdivisions", 500, authorization, ct, null, new Dictionary<string, object> { ["Id"] = id }); }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        { throw new EntityRuleException($"Недоступно зависимое подразделение API {id}. Проверьте область и головное подразделение."); }
        if (items.Count != 1 || items[0].Id != id) throw new EntityRuleException($"API не вернул требуемое подразделение {id}.");
        subdivisions.Add(id, items[0]);
      }
    }
    var pairs = subdivisions.Values.SelectMany(s => new[] { (s.StatusType, s.StatusCode), (s.OrganTypeId, s.OrganTypeCode) })
      .Concat(snapshot.Personnel.SelectMany(p => new[] { (p.PersonnelCategoryType, p.PersonnelCategoryCode), (p.SpecialRankType, p.SpecialRankCode),
        (p.PositionType, p.PositionCode), (p.WorkerCategoryType, p.WorkerCategoryCode), (p.StatusType, p.StatusCode) }))
      .Where(p => p.Item1.HasValue && p.Item2.HasValue).Select(p => (Type: p.Item1!.Value, Code: p.Item2!.Value)).Distinct();
    foreach (var key in pairs.Where(p => !knownClassifiers.Contains(p) && !classifiers.ContainsKey(p)))
    {
      ct.ThrowIfCancellationRequested(); CountRequest();
      logger.LogInformation("API: дозагрузка классификатора {ClassifierType}/{ClassifierCode}", key.Type, key.Code);
      List<RemoteClassifierDto> items;
      try { items = await FetchPages<RemoteClassifierDto>(address, "classifiers", 500, authorization, ct, null,
        new Dictionary<string, object> { ["ClassifierType"] = key.Type, ["ClassifierCode"] = key.Code }); }
      catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
      { throw new EntityRuleException($"Недоступен зависимый классификатор {key.Type}/{key.Code}."); }
      if (items.Count != 1 || items[0].Type != key.Type || items[0].Code != key.Code)
        throw new EntityRuleException($"API не вернул требуемый классификатор {key.Type}/{key.Code}.");
      classifiers.Add(key, items[0]);
    }
    logger.LogInformation("API: дозагрузка завершена, дополнительных запросов {DependencyRequestCount}", requests);
    return snapshot with { Subdivisions = subdivisions.Values.ToArray(), Classifiers = classifiers.Values.ToArray() };
  }

  private async Task<List<T>> FetchPages<T>(Uri address, string resource, int pageSize, string authorization, CancellationToken ct, DateOnly? updatedSince, IReadOnlyDictionary<string, object>? extraFilter = null)
  {
    var result = new List<T>();
    int? total = null, pages = null;
    for (var page = 1; ; page++)
    {
      using var pageScope = logger.BeginScope(new Dictionary<string, object> { ["ApiResource"] = resource, ["ApiPage"] = page, ["ApiPageSize"] = pageSize });
      Page<T>? body = null;
      for (var attempt = 0; attempt < 3; attempt++)
      {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(address, resource));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
          authorization);
        var filter = new Dictionary<string, object> { ["page"] = page, ["per_page"] = pageSize };
        if (updatedSince.HasValue) filter["UpdatedAt"] = updatedSince.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        if (extraFilter != null) foreach (var item in extraFilter) filter[item.Key] = item.Value;
        request.Content = JsonContent.Create(new { Data = filter }, options: Json);
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        logger.LogInformation("API: начало запроса {ApiResource}, страница {ApiPage}, размер {ApiPageSize}, попытка {ApiAttempt}, timeout {TimeoutSeconds} с", resource, page, pageSize, attempt + 1, http.Timeout.TotalSeconds);
        try
        {
          using var response = await http.SendAsync(request, ct);
          logger.LogInformation("API: получен ответ {ApiResource}, страница {ApiPage}, HTTP {ApiStatusCode}, длительность {ApiElapsedMs} мс", resource, page, (int)response.StatusCode, elapsed.Elapsed.TotalMilliseconds);
          if ((response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500) && attempt < 2)
          {
            await Task.Delay(TimeSpan.FromSeconds(attempt + 1), ct);
            continue;
          }
          response.EnsureSuccessStatusCode();
          body = await response.Content.ReadFromJsonAsync<Page<T>>(Json, ct);
          logger.LogInformation("API: прочитана страница {ApiResource} {ApiPage}, записей {ApiRecordCount}, Success {ApiSuccess}, всего страниц {ApiTotalPages}, всего записей {ApiTotalCount}", resource, page, body?.Data?.Count, body?.Success, body?.Pagination?.TotalPages, body?.Pagination?.TotalCount);
          break;
        }
        catch (OperationCanceledException ex)
        {
          logger.LogWarning("API: запрос отменён {ApiResource}, страница {ApiPage}, попытка {ApiAttempt}, длительность {ApiElapsedMs} мс, токен отменён {ApiTokenCancelled}, причина {ApiCancellationReason}, исключение {ExceptionType}", resource, page, attempt + 1, elapsed.Elapsed.TotalMilliseconds, ct.IsCancellationRequested, ct.IsCancellationRequested ? "OperationToken" : "HttpTimeoutOrTransportCancellation", ex.GetType().Name);
          throw;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == null && attempt < 2)
        {
          logger.LogWarning("API: сетевая ошибка {ApiResource}, страница {ApiPage}, попытка {ApiAttempt}, длительность {ApiElapsedMs} мс; повтор запроса, исключение {ExceptionType}", resource, page, attempt + 1, elapsed.Elapsed.TotalMilliseconds, ex.GetType().Name);
          await Task.Delay(TimeSpan.FromSeconds(attempt + 1), ct);
        }
      }
      if (body is not { Success: true, Data: not null, Pagination: not null })
        throw new EntityRuleException("API вернул неполную или неуспешную страницу.");
      var p = body.Pagination;
      var expectedPages = Math.Max(1, (int)Math.Ceiling((double)p.TotalCount / pageSize));
      if (p.TotalCount < 0 || p.TotalCount > 1_000_000 || p.Page != page ||
          p.PerPage != pageSize || p.TotalPages != expectedPages && !(p.TotalCount == 0 && p.TotalPages == 0) ||
          total.HasValue && (total != p.TotalCount || pages != p.TotalPages))
        throw new EntityRuleException("Пагинация API изменилась или содержит противоречивые значения.");
      total = p.TotalCount; pages = p.TotalPages;
      var expectedCount = Math.Min(pageSize, p.TotalCount - (page - 1) * pageSize);
      if (body.Data.Count != expectedCount)
        throw new EntityRuleException("Количество записей страницы API не соответствует пагинации.");
      result.AddRange(body.Data);
      if (page >= p.TotalPages) break;
    }
    logger.LogInformation("API: ресурс {ApiResource} загружен полностью, записей {ApiRecordCount}", resource, result.Count);
    return result;
  }
}
