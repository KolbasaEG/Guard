namespace Guard.Core.Services.DTOs;

public sealed record ApiDiagnosticIssue(string Resource, string RecordId, string Field, string Code,
  string? ReferenceId = null, bool FoundLocally = false);

public sealed record ApiDiagnosticResult(Guid DiagnosticRunId, bool FetchComplete, bool AnalysisComplete,
  bool Cancelled, int Classifiers, int Subdivisions, int Personnel, int Issues, string? Error = null, int ExitEvents = 0);
