using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

public class ClassifierService : IClassifierService
{
  private readonly IReadRepository<Classifier> _readClassifierRepository;
  private readonly IUnitOfWork _uow;
  private readonly ILogger<ClassifierService> _logger;

  public ClassifierService(
      IReadRepository<Classifier> readClassifierRepository,
      IUnitOfWork unitOfWork,
      ILogger<ClassifierService> logger)
  {
    _readClassifierRepository = readClassifierRepository;
    _uow = unitOfWork;
    _logger = logger;
  }

  // ==================== Read (Изолированный IReadRepository) ====================

  public async Task<TResult> QueryClassifiersAsync<TResult>(
      Func<IQueryable<Classifier>, Task<TResult>> query,
      CancellationToken ct = default)
  {
    return await _readClassifierRepository.QueryAsync(query, ct);
  }

  public async Task<Classifier?> GetByIdAsync(int id, CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос классификатора по ID: {ClassifierId}", id);
    return await _readClassifierRepository.QueryAsync(query =>
        query.FirstOrDefaultAsync(c => c.Id == id, ct), ct);
  }

  public async Task<IReadOnlyList<Classifier>> GetByTypeAsync(int type, bool activeOnly = true, CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос классификаторов по типу: {Type} (ActiveOnly: {ActiveOnly})", type, activeOnly);
    return await _readClassifierRepository.QueryAsync(query =>
        query.Where(c => c.Type == type && (!activeOnly || c.IsActive))
             .OrderBy(c => c.Code)
             .ToListAsync(ct),
        ct);
  }

  public async Task<IReadOnlyList<Classifier>> GetByClassifierNameAsync(string classifierName, bool activeOnly = true, CancellationToken ct = default)
  {
    if (string.IsNullOrWhiteSpace(classifierName))
      return Array.Empty<Classifier>();

    var trimmedName = classifierName.Trim();

    _logger.LogDebug("Запрос классификаторов по наименованию: '{ClassifierName}'", trimmedName);
    return await _readClassifierRepository.QueryAsync(query =>
        query.Where(c => c.ClassifierName == trimmedName && (!activeOnly || c.IsActive))
             .OrderBy(c => c.Code)
             .ToListAsync(ct),
        ct);
  }

  public async Task<IReadOnlyList<Classifier>> GetAllAsync(CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос всех записей классификатора");
    return await _readClassifierRepository.QueryAsync(query =>
        query.OrderBy(c => c.Type)
             .ThenBy(c => c.Code)
             .ToListAsync(ct),
        ct);
  }

  public async Task<bool> IsCodeUniqueInTypeAsync(int type, int code, int? excludeId = null, CancellationToken ct = default)
  {
    return await _readClassifierRepository.QueryAsync(query =>
        query.AllAsync(c => c.Type != type || c.Code != code || (excludeId.HasValue && c.Id == excludeId.Value), ct),
        ct);
  }

  // ==================== Write (IUnitOfWork & BasicRepository) ====================

  public async Task<int> CreateAsync(Classifier classifier, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(classifier);

    return await _uow.ExecuteInTransactionAsync(async () =>
    {
      classifier.ClassifierName = classifier.ClassifierName?.Trim() ?? string.Empty;
      classifier.Value = classifier.Value?.Trim() ?? string.Empty;
      classifier.UpdatedAt = DateTime.UtcNow;

      await _uow.BasicRepository<Classifier>().AddAsync(classifier, ct);
      await _uow.SaveChangesAsync(ct);

      _logger.LogInformation("Создана новая запись классификатора '{Value}' (Type: {Type}, Code: {Code}, ID: {ClassifierId})",
          classifier.Value, classifier.Type, classifier.Code, classifier.Id);

      return classifier.Id;
    }, ct);
  }

  public async Task UpdateAsync(Classifier classifier, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(classifier);

    classifier.ClassifierName = classifier.ClassifierName?.Trim() ?? string.Empty;
    classifier.Value = classifier.Value?.Trim() ?? string.Empty;
    classifier.UpdatedAt = DateTime.UtcNow;

    await _uow.BasicRepository<Classifier>().UpdateAsync(classifier, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Обновлена запись классификатора '{Value}' (Type: {Type}, Code: {Code}, ID: {ClassifierId})",
        classifier.Value, classifier.Type, classifier.Code, classifier.Id);
  }

  public async Task SetActiveStatusAsync(int id, bool isActive, CancellationToken ct = default)
  {
    var classifier = await GetRequiredForWriteAsync(id, ct);

    classifier.IsActive = isActive;
    classifier.UpdatedAt = DateTime.UtcNow;

    await _uow.BasicRepository<Classifier>().UpdateAsync(classifier, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Статус активности классификатора ID {ClassifierId} изменен на: {IsActive}", id, isActive);
  }

  public async Task DeleteAsync(int id, CancellationToken ct = default)
  {
    var classifier = await GetRequiredForWriteAsync(id, ct);

    await _uow.BasicRepository<Classifier>().DeleteAsync(classifier, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogWarning("Запись классификатора '{Value}' (ID: {ClassifierId}) была физически удалена из системы",
        classifier.Value, id);
  }

  // ==================== Private Helpers ====================

  private async Task<Classifier> GetRequiredForWriteAsync(int id, CancellationToken ct)
  {
    var classifier = await _uow.BasicRepository<Classifier>().GetByIdAsync([id], ct);

    if (classifier == null)
    {
      _logger.LogWarning("Попытка выполнения операции над несуществующей записью классификатора (ID: {ClassifierId})", id);
      throw new KeyNotFoundException($"Запись классификатора с ID '{id}' не найдена.");
    }

    return classifier;
  }
}