using Guard.Core.Services.DTOs;

namespace Guard.Components.Pages.Logs;

public sealed record TechnicalLoggingHeaderState(TechnicalLogHealth? Health = null, bool Busy = false, string? Error = null);
