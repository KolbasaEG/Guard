namespace Guard.Core.Services.DTOs;
// Только скалярные поля сотрудника и названия связанных справочных значений.
// Связанные Identity-объекты (пароли, stamp и claims) в карточку не передаются.
public record PersonalDetailsDto(Guid Id, IReadOnlyDictionary<string,object?> Fields);
