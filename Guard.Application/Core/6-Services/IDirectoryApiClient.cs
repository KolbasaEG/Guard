using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

public interface IDirectoryApiClient
{
  Task<ApiSnapshot> FetchAsync(CancellationToken ct);
  Task<ApiSnapshot> FetchAsync(string baseUrl, int pageSize, CancellationToken ct, string? username = null, string? password = null, DateOnly? updatedSince = null, long? headSubdivisionId = null, Func<string, CancellationToken, Task>? phaseChanged = null);
  Task<ApiSnapshot> FetchDiagnosticAsync(string baseUrl, int pageSize, string username, string password, DateOnly updatedSince, CancellationToken ct, IProgress<string>? progress = null);
  Task<ApiSnapshot> ResolveDependenciesAsync(ApiSnapshot snapshot, IReadOnlySet<(int Type, int Code)> knownClassifiers,
    IReadOnlySet<long> knownSubdivisions, string baseUrl, string username, string password, CancellationToken ct);
  Task PingAsync(string baseUrl, string username, string password, CancellationToken ct);
}
