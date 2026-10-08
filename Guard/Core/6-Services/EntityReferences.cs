using Guard.Core.Entities;
using Guard.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

internal static class EntityReferences
{
  private static async Task ClassifierAsync(IUnitOfWork work, int? type, int? code,
      int? previousType, int? previousCode, CancellationToken ct)
  {
    if (!type.HasValue) return;
    var unchanged = type == previousType && code == previousCode;
    if (!await work.BasicRepository<Classifier>().Query().AnyAsync(c => c.Type == type && c.Code == code && (unchanged || c.IsActive), ct))
      throw new ArgumentException("Выберите существующее активное справочное значение.");
  }

  public static async Task PersonalAsync(IUnitOfWork work, Personal input, Personal? previous, CancellationToken ct)
  {
    if (input.SubdivisionId.HasValue && input.SubdivisionId != previous?.SubdivisionId &&
        !await work.BaseEntityRepository<Subdivision>().Query().AnyAsync(s => s.Id == input.SubdivisionId &&
          (s.Status == Status.Inserted || s.Status == Status.Modified), ct))
      throw new ArgumentException("Выберите активное подразделение.");
    await ClassifierAsync(work, input.PersonnelCategoryType, input.PersonnelCategoryCode, previous?.PersonnelCategoryType, previous?.PersonnelCategoryCode, ct);
    await ClassifierAsync(work, input.SpecialRankType, input.SpecialRankCode, previous?.SpecialRankType, previous?.SpecialRankCode, ct);
    await ClassifierAsync(work, input.PositionType, input.PositionCode, previous?.PositionType, previous?.PositionCode, ct);
    await ClassifierAsync(work, input.WorkerCategoryType, input.WorkerCategoryCode, previous?.WorkerCategoryType, previous?.WorkerCategoryCode, ct);
    await ClassifierAsync(work, input.StatusType, input.StatusCode, previous?.StatusType, previous?.StatusCode, ct);
  }

  public static async Task SubdivisionAsync(IUnitOfWork work, Subdivision input, Subdivision? previous, CancellationToken ct)
  {
    await ClassifierAsync(work, input.StatusType, input.StatusCode, previous?.StatusType, previous?.StatusCode, ct);
    if (input.OrganTypeId.HasValue && !await work.BasicRepository<OrganType>().Query()
        .AnyAsync(o => o.Id == input.OrganTypeId && o.Code == input.OrganTypeCode, ct))
      throw new ArgumentException("Выберите существующий тип органа.");
  }

  public static Task OrganTypeAsync(IUnitOfWork work, OrganType input, OrganType? previous, CancellationToken ct) =>
    ClassifierAsync(work, input.ClassifierType, input.Code, previous?.ClassifierType, previous?.Code, ct);
}
