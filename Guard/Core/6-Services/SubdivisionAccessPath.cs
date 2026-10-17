namespace Guard.Core.Services;

public static class SubdivisionAccessPath
{
  public static bool IsValid(string? path) => path != null && path.StartsWith('/') && path.EndsWith('/') &&
    path.Split('/', StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } segments &&
    segments.All(s => long.TryParse(s, out var value) && value > 0);
}
