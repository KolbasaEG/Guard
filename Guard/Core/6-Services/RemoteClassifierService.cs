using Guard.Core.Entities;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Guard.Core.Services;

public class RemoteClassifierService : IRemoteClassifierService
{
  private readonly HttpClient _httpClient;
  private readonly IUnitOfWork _uow;
  private readonly ILogger<RemoteClassifierService> _logger;

  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNameCaseInsensitive = true
  };

  public RemoteClassifierService(
      HttpClient httpClient,
      IUnitOfWork uow,
      ILogger<RemoteClassifierService> logger)
  {
    _httpClient = httpClient;
    if (_httpClient.BaseAddress == null)
    {
      _httpClient.BaseAddress = new Uri("https://a.todes.by:13640/face-department-service/api/classifiers");
    }

    if (_httpClient.DefaultRequestHeaders.Authorization == null)
    {
      var authBytes = Encoding.UTF8.GetBytes("admin:admin123");
      _httpClient.DefaultRequestHeaders.Authorization =
          new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
    }

    _uow = uow;
    _logger = logger;
  }

  public async Task<IReadOnlyList<RemoteClassifierDto>> FetchClassifiersAsync(CancellationToken ct = default)
  {
    var allClassifiers = new List<RemoteClassifierDto>();
    int currentPage = 1;
    int totalPages = 1;
    const int perPage = 500;

    try
    {
      _logger.LogInformation("Запуск получения классификаторов с внешнего сервиса...");

      do
      {
        var requestBody = new ClassifierApiRequest
        {
          Data = new ClassifierFilterData
          {
            Page = currentPage,
            PerPage = perPage
          }
        };

        var response = await _httpClient.PostAsJsonAsync("classifiers", requestBody, JsonOptions, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ClassifierApiResponse>(JsonOptions, ct);

        if (result == null || !result.Success)
        {
          _logger.LogWarning("Запрос страницы {Page} вернул неуспешный ответ (Success = false)", currentPage);
          break;
        }

        if (result.Data != null && result.Data.Any())
        {
          allClassifiers.AddRange(result.Data);
        }

        totalPages = result.Pagination?.TotalPages ?? 1;
        currentPage++;

      } while (currentPage <= totalPages);

      _logger.LogInformation("Успешно загружено {Count} классификаторов с {TotalPages} страниц", allClassifiers.Count, totalPages);

      return allClassifiers;
    }
    catch (HttpRequestException ex)
    {
      _logger.LogError(ex, "Ошибка при совершении HTTP-запроса к внешнему ресурсу классификаторов");
      throw;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Непредвиденная ошибка при получении удаленных данных классификаторов");
      throw;
    }
  }

  public async Task SyncClassifiersAsync(CancellationToken ct = default)
  {
    var remoteItems = await FetchClassifiersAsync(ct);

    if (!remoteItems.Any())
    {
      _logger.LogWarning("От удаленного ресурса получен пустой список классификаторов. Синхронизация отменена.");
      return;
    }

    // -----------------------------------------------------------------
    // Сброс Sequence в PostgreSQL перед вставкой записей
    // Гарантирует, что следующий сгенерированный Id не будет конфликтовать с уже существующими в БД
    // -----------------------------------------------------------------
    await FixSequenceAsync(ct);


    await _uow.ExecuteInTransactionAsync(async () =>
    {
      var repository = _uow.BasicRepository<Classifier>();

      // Загружаем существующие записи
      var localClassifiers = await repository.Query().ToListAsync(ct);

      // -----------------------------------------------------------------
      // 1. ЕСЛИ БАЗА ДАННЫХ ПУСТА: выполняем чистую вставку всех элементов
      // -----------------------------------------------------------------
      if (!localClassifiers.Any())
      {
        _logger.LogInformation("Локальная таблица классификаторов пуста. Выполняется первичная загрузка данных...");

        var newEntities = remoteItems.Select(dto => new Classifier
        {
          Type = dto.Type,
          Code = dto.Code,
          ClassifierName = dto.ClassifierName,
          Value = dto.Value,
          IsActive = dto.IsActiveBool,
          UpdatedAt = DateTime.SpecifyKind(dto.UpdatedAt, DateTimeKind.Utc)
        }).ToList();

        foreach (var entity in newEntities)
        {
          await repository.AddAsync(entity, ct);
        }

        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("Первичная загрузка успешно завершена. Добавлено элементов: {Count}", newEntities.Count);
        return;
      }

      // -----------------------------------------------------------------
      // 2. ЕСЛИ В БД ЕСТЬ ДАННЫЕ: добавляем отсутствующие, обновляем измененные
      // -----------------------------------------------------------------
      var localDict = localClassifiers.ToDictionary(c => (c.Type, c.Code));

      int addedCount = 0;
      int updatedCount = 0;

      foreach (var dto in remoteItems)
      {
        var key = (dto.Type, dto.Code);
        var remoteUtcDate = DateTime.SpecifyKind(dto.UpdatedAt, DateTimeKind.Utc);

        if (!localDict.TryGetValue(key, out var existing))
        {
          // Элемент отсутствует в локальной БД -> Добавляем
          var newClassifier = new Classifier
          {
            Type = dto.Type,
            Code = dto.Code,
            ClassifierName = dto.ClassifierName,
            Value = dto.Value,
            IsActive = dto.IsActiveBool,
            UpdatedAt = remoteUtcDate
          };

          await repository.AddAsync(newClassifier, ct);
          addedCount++;
        }
        else
        {
          // Проверяем, изменились ли данные у существующего элемента
          bool isModified = existing.ClassifierName != dto.ClassifierName ||
                            existing.Value != dto.Value ||
                            existing.IsActive != dto.IsActiveBool ||
                            existing.UpdatedAt != remoteUtcDate;

          if (isModified)
          {
            existing.ClassifierName = dto.ClassifierName;
            existing.Value = dto.Value;
            existing.IsActive = dto.IsActiveBool;
            existing.UpdatedAt = remoteUtcDate;

            await repository.UpdateAsync(existing, ct);
            updatedCount++;
          }
        }
      }

      // Сохраняем изменения только при их наличии
      if (addedCount > 0 || updatedCount > 0)
      {
        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("Синхронизация классификаторов завершена. Добавлено: {Added}, обновлено: {Updated}", addedCount, updatedCount);
      }
      else
      {
        _logger.LogInformation("Синхронизация классификаторов завершена. Изменений не обнаружено.");
      }
    }, ct);
  }
  /// <summary>
  /// Синхронизирует значение PostgreSQL Sequence с фактическим максимальным Id в таблице Classifiers.
  /// </summary>
  private async Task FixSequenceAsync(CancellationToken ct)
  {
    const string sql = @"
      SELECT setval(
          pg_get_serial_sequence('""Classifiers""', 'Id'), 
          COALESCE(MAX(""Id""), 1)
      ) 
      FROM ""Classifiers"";";

    // Вариант 1: Если в IUnitOfWork или DbContext есть метод выполнения SQL
    if (_uow is DbContext dbContext)
    {
      await dbContext.Database.ExecuteSqlRawAsync(sql, ct);
    }
    else
    {
      // Вариант 2: Вызов через метод ExecuteSqlRawAsync у интерфейса IUnitOfWork (если такой метод добавлен в IUnitOfWork)
      await _uow.ExecuteSqlRawAsync(sql, ct);
    }
  }
}