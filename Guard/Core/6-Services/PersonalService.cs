using Guard.Core.Identity;
using Guard.Core.Contexts;
using Guard.Core.Services.DTOs;
using System.Text;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

public class PersonalService : IPersonalService
{
  private readonly IReadRepository<Personal> _readPersonalRepository;
  private readonly IReadRepository<Subdivision> _readSubdivisionRepository;
  private readonly IUnitOfWork _uow;
  private readonly ILogger<PersonalService> _logger;
  private readonly IDbContextFactory<ApplicationDbContext> _factory;
  private readonly IPermissionService _permissions;
  private readonly IDataAccessScopeService _scopes;

  public PersonalService(
      IReadRepository<Personal> readPersonalRepository,
      IReadRepository<Subdivision> readSubdivisionRepository, IUnitOfWork unitOfWork,
      ILogger<PersonalService> logger, IPermissionService permissions, IDataAccessScopeService scopes, IDbContextFactory<ApplicationDbContext> factory)
  {
    _readPersonalRepository = readPersonalRepository;
    _readSubdivisionRepository = readSubdivisionRepository;

    _uow = unitOfWork;
    _logger = logger;
    _factory = factory;
    _permissions = permissions;
    _scopes = scopes;
  }
  public async Task<IReadOnlyList<Subdivision>> GetAllActiveSubdivisionsAsync(CancellationToken ct = default)
  {
    await _permissions.RequireAsync(Permissions.Personals.Write, ct);
    var scope = await _scopes.GetAsync(ct);
    await using var db = await _factory.CreateDbContextAsync(ct);
    return await db.Set<Subdivision>().AsNoTracking()
      .Where(s => (scope.IsRoot || scope.SubdivisionIds.Contains(s.Id)) &&
        (s.Status == Status.Inserted || s.Status == Status.Modified))
      .OrderBy(s => s.Name).ToListAsync(ct);
  }

  // ==================== Read (Изолированный IReadRepository) ====================

  public async Task<TResult> QueryPersonalsAsync<TResult>(
      Func<IQueryable<Personal>, Task<TResult>> query,
      CancellationToken ct = default)
  {
    return await _readPersonalRepository.QueryAsync(query, ct);
  }

  public async Task<Personal?> GetByIdAsync(Guid id, CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос персонала по ID: {PersonalId}", id);
    return await _readPersonalRepository.QueryAsync(q => q.Include(p => p.Subdivision)
      .Include(p => p.PersonnelCategory).Include(p => p.SpecialRank).Include(p => p.Position)
      .Include(p => p.WorkerCategory).Include(p => p.StatusClassifier).Include(p => p.IpAddresses)
      .SingleOrDefaultAsync(p => p.Id == id, ct), ct);
  }

  public async Task<IReadOnlyList<Personal>> GetAllActiveAsync(CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос всех активных сотрудников");
    return await _readPersonalRepository.QueryAsync(query =>
        query.Where(p => p.Status != Status.Deleted && p.Status != Status.Archived)
             .OrderBy(p => p.LastName)
             .ThenBy(p => p.FirstName)
             .ToListAsync(ct),
        ct);
  }

  public async Task<IReadOnlyList<Personal>> GetBySubdivisionIdAsync(Guid subdivisionId, CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос персонала для подразделения с ID: {SubdivisionId}", subdivisionId);
    return await _readPersonalRepository.QueryAsync(query =>
        query.Where(p => p.SubdivisionId == subdivisionId && p.Status != Status.Deleted)
             .OrderBy(p => p.LastName)
             .ThenBy(p => p.FirstName)
             .ToListAsync(ct),
        ct);
  }

  public async Task<IReadOnlyList<Personal>> GetBySubdivisionPathAsync(
      string targetPath,
      SubdivisionHierarchyMode mode = SubdivisionHierarchyMode.IncludeChildren,
      CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос персонала для пути подразделения '{TargetPath}' с режимом {HierarchyMode}", targetPath, mode);
    return await _readPersonalRepository.QueryAsync(query =>
        query.Where(p => p.Status != Status.Deleted && p.Status != Status.Archived)
             .FilterBySubdivision(targetPath, mode)
             .OrderBy(p => p.LastName)
             .ThenBy(p => p.FirstName)
             .ToListAsync(ct),
        ct);
  }

  // ==================== Write (IUnitOfWork & ChangeTracker) ====================

  public async Task<Guid> CreateAsync(Personal Personal, CancellationToken ct = default)
  {
    await _permissions.RequireAsync(Permissions.Personals.Write, ct);
    ArgumentNullException.ThrowIfNull(Personal);

    if (!(await _scopes.GetAsync(ct)).Allows(Personal.SubdivisionId)) throw new UnauthorizedAccessException("Подразделение недоступно.");
    Personal.Status = Status.Inserted;

    await _uow.BaseEntityRepository<Personal>().AddAsync(Personal, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Создана новая запись сотрудника '{LastName} {FirstName}' с ID: {PersonalId}",
        Personal.LastName, Personal.FirstName, Personal.Id);

    return Personal.Id;
  }

  public async Task UpdateAsync(Personal Personal, CancellationToken ct = default)
  {
    await _permissions.RequireAsync(Permissions.Personals.Write, ct);
    ArgumentNullException.ThrowIfNull(Personal);

    var current = await _readPersonalRepository.GetByIdAsync(Personal.Id, ct) ?? throw new KeyNotFoundException("Сотрудник недоступен.");
    if (current.Status is not (Status.Inserted or Status.Modified)) throw new InvalidOperationException("Сотрудник заблокирован или неактивен.");
    if (!(await _scopes.GetAsync(ct)).Allows(Personal.SubdivisionId)) throw new UnauthorizedAccessException("Подразделение недоступно.");
    Personal.Status = current.Status;
    Personal.UpdatedAt = DateTime.UtcNow;

    await _uow.BaseEntityRepository<Personal>().UpdateAsync(Personal, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Обновлены данные сотрудника '{LastName} {FirstName}' (ID: {PersonalId})",
        Personal.LastName, Personal.FirstName, Personal.Id);
  }

  // ==================== Status Management ====================

  public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
  {
    await _permissions.RequireAsync(Permissions.Personals.Write, ct);
    var Personal = await GetRequiredForWriteAsync(id, ct);

    await _uow.BaseEntityRepository<Personal>().SoftDeleteAsync(Personal, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogWarning("Сотрудник '{LastName} {FirstName}' (ID: {PersonalId}) помечен как удаленный",
        Personal.LastName, Personal.FirstName, id);
  }

  public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
  {
    await _permissions.RequireAsync(Permissions.Personals.Write, ct);
    var Personal = await GetRequiredForWriteAsync(id, ct);

    await _uow.BaseEntityRepository<Personal>().ArchiveAsync(Personal, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Сотрудник '{LastName} {FirstName}' (ID: {PersonalId}) отправлен в архив",
        Personal.LastName, Personal.FirstName, id);
  }

  public async Task BlockAsync(Guid id, CancellationToken ct = default)
  {
    await _permissions.RequireAsync(Permissions.Personals.Write, ct);
    var Personal = await GetRequiredForWriteAsync(id, ct);

    await _uow.BaseEntityRepository<Personal>().BlockAsync(Personal, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogWarning("Сотрудник '{LastName} {FirstName}' (ID: {PersonalId}) заблокирован",
        Personal.LastName, Personal.FirstName, id);
  }

  public async Task UnblockAsync(Guid id, CancellationToken ct = default)
  {
    await _permissions.RequireAsync(Permissions.Personals.Write, ct);
    var Personal = await GetRequiredForWriteAsync(id, ct);

    await _uow.BaseEntityRepository<Personal>().UnblockAsync(Personal, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Сотрудник '{LastName} {FirstName}' (ID: {PersonalId}) разблокирован",
        Personal.LastName, Personal.FirstName, id);
  }

  public async Task RestoreAsync(Guid id, CancellationToken ct = default)
  {
    await _permissions.RequireAsync(Permissions.Personals.Write, ct);
    var Personal = await GetRequiredForWriteAsync(id, ct);

    await _uow.BaseEntityRepository<Personal>().RestoreAsync(Personal, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Сотрудник '{LastName} {FirstName}' (ID: {PersonalId}) восстановлен",
        Personal.LastName, Personal.FirstName, id);
  }

  // ==================== Private Helpers ====================

  private async Task<Personal> GetRequiredForWriteAsync(Guid id, CancellationToken ct)
  {
    var visible = await _readPersonalRepository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Сотрудник недоступен.");
    var Personal = await _uow.BaseEntityRepository<Personal>().GetByIdAsync(id, ct);

    if (Personal == null)
    {
      _logger.LogWarning("Попытка выполнения операции над несуществующей записью персонала (ID: {PersonalId})", id);
      throw new KeyNotFoundException($"Сотрудник с ID '{id}' не найден.");
    }

    return Personal;
  }

  private async Task<IQueryable<Personal>> ListQueryAsync(ApplicationDbContext db, PersonalSearchRequest request, CancellationToken ct)
  {
    await _permissions.RequireAsync(Permissions.Personals.Read, ct);
    var scope = await _scopes.GetAsync(ct);
    var q = db.Set<Personal>().AsNoTracking().AsQueryable();
    if (!scope.IsRoot) q = q.Where(p => p.SubdivisionId.HasValue && scope.SubdivisionIds.Contains(p.SubdivisionId.Value));
    switch (request.Mode)
    {
      case DataViewMode.Active: q = q.Where(p => p.Status == Status.Inserted || p.Status == Status.Modified || p.Status == Status.Blocked); break;
      case DataViewMode.Archived:
        await _permissions.RequireAsync(Permissions.Personals.ReadArchive, ct);
        q = q.Where(p => p.Status == Status.Archived || p.Status == Status.ArchivedBlocked); break;
      case DataViewMode.Deleted:
        if (!scope.IsRoot) throw new UnauthorizedAccessException("Удалённые записи доступны только Root.");
        q = q.Where(p => p.Status == Status.Deleted); break;
      default: throw new ArgumentException("Неизвестный режим просмотра.");
    }
    if (request.SubdivisionId is Guid id)
    {
      if (!scope.Allows(id)) throw new UnauthorizedAccessException("Подразделение недоступно.");
      var path = await db.Set<Subdivision>().Where(s => s.Id == id).Select(s => s.Path).SingleOrDefaultAsync(ct);
      if (string.IsNullOrEmpty(path)) return q.Where(p => false);
      q = request.IncludeChildren ? q.Where(p => p.Subdivision != null && p.Subdivision.Path.StartsWith(path)) : q.Where(p => p.SubdivisionId == id);
    }
    if (!string.IsNullOrWhiteSpace(request.Search))
    {
      var term = request.Search.Trim().ToLower();
      q = q.Where(p => p.LastName.ToLower().Contains(term) || p.FirstName.ToLower().Contains(term) ||
        (p.MiddleName != null && p.MiddleName.ToLower().Contains(term)));
    }
    // Сортировка только по полям безопасного списка, без Dynamic LINQ.
    return request.OrderBy switch {
      "FirstName" or "FirstName asc" => q.OrderBy(p => p.FirstName).ThenBy(p => p.Id),
      "FirstName desc" => q.OrderByDescending(p => p.FirstName).ThenBy(p => p.Id),
      "FullName" or "FullName asc" => q.OrderBy(p => p.FullName).ThenBy(p => p.Id),
      "FullName desc" => q.OrderByDescending(p => p.FullName).ThenBy(p => p.Id),
      "SubdivisionName" or "SubdivisionName asc" => q.OrderBy(p => p.Subdivision!.Name).ThenBy(p => p.Id),
      "SubdivisionName desc" => q.OrderByDescending(p => p.Subdivision!.Name).ThenBy(p => p.Id),
      "LastName desc" => q.OrderByDescending(p => p.LastName).ThenBy(p => p.FirstName).ThenBy(p => p.Id),
      _ => q.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ThenBy(p => p.Id)
    };
  }
  private static IQueryable<PersonalListItemDto> ProjectList(IQueryable<Personal> query) =>
    query.Select(p => new PersonalListItemDto(p.Id, p.LastName, p.FirstName, p.MiddleName, p.FullName,
        p.Subdivision != null ? p.Subdivision.Name : null, p.Status));

  public async Task<PersonalSearchResult> SearchAsync(PersonalSearchRequest request, CancellationToken ct = default)
  {
    if (request.Skip < 0 || request.Take is < 1 or > 100) throw new ArgumentException("Недопустимые параметры страницы.");
    await using var db = await _factory.CreateDbContextAsync(ct);
    var q = await ListQueryAsync(db, request, ct);
    var count = await q.CountAsync(ct);
    var items = await ProjectList(q.Skip(request.Skip).Take(request.Take)).ToListAsync(ct);
    return new(items, count);
  }
  public async Task<byte[]> ExportAsync(PersonalSearchRequest request, CancellationToken ct = default)
  {
    await _permissions.RequireAsync(Permissions.Personals.Export, ct);
    await using var db = await _factory.CreateDbContextAsync(ct);
    var items = await ProjectList(await ListQueryAsync(db, request, ct)).ToListAsync(ct);
    static string Cell(string? value)
    {
      value ??= "";
      // Excel не должен исполнять пользовательское значение как формулу.
      if (value.Length > 0 && "=+-@\t\r".Contains(value[0])) value = "'" + value;
      return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
    var csv = new StringBuilder("Фамилия;Имя;Отчество;Подразделение\r\n");
    foreach (var item in items) csv.AppendLine(string.Join(";", Cell(item.LastName), Cell(item.FirstName), Cell(item.MiddleName), Cell(item.SubdivisionName)));
    return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
  }

  public Task<PersonalDetailsDto?> GetDetailsAsync(Guid id, CancellationToken ct = default) =>
    _readPersonalRepository.QueryAsync(async q =>
    {
      var row = await q.Include(p => p.Subdivision).Include(p => p.PersonnelCategory).Include(p => p.SpecialRank)
        .Include(p => p.Position).Include(p => p.WorkerCategory).Include(p => p.StatusClassifier).Include(p => p.IpAddresses)
        .Where(p => p.Id == id).Select(p => new { Personal = p, UserId = p.User == null ? null : p.User.Id,
          UserName = p.User == null ? null : p.User.UserName }).SingleOrDefaultAsync(ct);
      if (row == null) return null;
      var personal = row.Personal;
      var fields = typeof(Personal).GetProperties().Where(p => p.PropertyType == typeof(string) || p.PropertyType.IsValueType)
        .ToDictionary(p => p.Name, p => p.GetValue(personal));
      fields["SubdivisionName"] = personal.Subdivision?.Name;
      fields["PersonnelCategoryName"] = personal.PersonnelCategory?.Value;
      fields["SpecialRankName"] = personal.SpecialRank?.Value;
      fields["PositionName"] = personal.Position?.Value;
      fields["WorkerCategoryName"] = personal.WorkerCategory?.Value;
      fields["StatusClassifierName"] = personal.StatusClassifier?.Value;
      fields["IpAddresses"] = string.Join(", ", personal.IpAddresses.Select(ip => ip.Address));
      fields["UserId"] = row.UserId;
      fields["UserName"] = row.UserName;
      return new PersonalDetailsDto(id, fields);
    }, ct);
}
