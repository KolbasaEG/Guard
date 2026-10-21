using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Guard.Core.Logging;

/// <summary>Ограниченный граф исключений без Message, Data, SQL, адресов и путей исходников.</summary>
public static partial class TechnicalException
{
  public const int MaximumNodes = 8;
  public const int MaximumLength = 8192;
  public static string Describe(Exception error)
  {
    var output = new StringBuilder("GuardExceptionChain/v1\n");
    var queue = new Queue<(Exception Error, int Parent, int Depth)>();
    var seen = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
    queue.Enqueue((error, -1, 0));
    var count = 0; var truncated = false;
    while (queue.TryDequeue(out var item)) {
      if (!seen.Add(item.Error)) continue;
      if (count == MaximumNodes || item.Depth == MaximumNodes) { truncated = true; break; }
      var index = count++;
      output.AppendLine($"Exception[{index}] Parent={item.Parent} Type={item.Error.GetType().FullName} Code={Code(item.Error) ?? "-"}");
      // false исключает имена файлов и абсолютные пути сборочной машины.
      output.AppendLine(TechnicalLogPrivacy.Text(new StackTrace(item.Error, false).ToString(), 700));
      if (item.Error is AggregateException aggregate) {
        foreach (var child in aggregate.InnerExceptions.Take(MaximumNodes)) queue.Enqueue((child, index, item.Depth + 1));
        if (aggregate.InnerExceptions.Count > MaximumNodes) truncated = true;
      } else if (item.Error.InnerException is { } inner) queue.Enqueue((inner, index, item.Depth + 1));
    }
    if (truncated) output.AppendLine("[ExceptionChainTruncated]");
    return TechnicalLogPrivacy.Text(output.ToString(), MaximumLength);
  }
  public static string? Code(Exception error) => error switch {
    Npgsql.PostgresException pg => "SQLSTATE:" + pg.SqlState,
    System.Net.Sockets.SocketException socket => "Socket:" + (int)socket.SocketErrorCode,
    HttpRequestException { StatusCode: { } status } => "HTTP:" + (int)status,
    TimeoutException => "Timeout", OperationCanceledException => "Cancelled", _ => null
  };
  public static string? FirstCode(Exception error)
  {
    var queue = new Queue<Exception>(); queue.Enqueue(error); var count = 0;
    while (queue.TryDequeue(out var item) && count++ < MaximumNodes) {
      if (Code(item) is { } code) return code;
      if (item is AggregateException aggregate) foreach (var child in aggregate.InnerExceptions.Take(MaximumNodes)) queue.Enqueue(child);
      else if (item.InnerException is { } inner) queue.Enqueue(inner);
    }
    return null;
  }
  public static string? ForExport(string? value)
  {
    if (value == null) return null;
    // Не доверяем старым строкам: их сообщения могли содержать секреты. Заголовки допускаются только в новом формате.
    var modern = value.StartsWith("GuardExceptionChain/v1\n", StringComparison.Ordinal);
    return TechnicalLogPrivacy.Text(string.Join('\n', value[..Math.Min(value.Length, MaximumLength)].Split('\n')
      .Where(line => line.TrimStart().StartsWith("at ", StringComparison.Ordinal) ||
        modern && (Header().IsMatch(line.TrimEnd('\r')) || line is "GuardExceptionChain/v1" or "[ExceptionChainTruncated]"))
      .Take(80)), MaximumLength);
  }
  [GeneratedRegex(@"^Exception\[[0-7]\] Parent=-?[0-7] Type=[A-Za-z0-9_.+`]+ Code=(-|Timeout|Cancelled|SQLSTATE:[A-Z0-9]{5}|Socket:[0-9]+|HTTP:[0-9]{3})$")]
  private static partial Regex Header();
}
