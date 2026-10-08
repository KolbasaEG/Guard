using Guard.Core.Entities;
using Radzen;
using Radzen.Blazor;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;

namespace Guard.Components.Pages.Administrator.Users;

// Снимок условий фильтра. После «Применить» правки конструктора не меняют запрос.
public static class UserListQuery
{
  private static readonly HashSet<string> Fields = ["Id", "UserName", "Email", "PersonalId", "AccountBlockReason", "LastActivityAtUtc", "MustChangePassword"];

  public static Func<IQueryable<ApplicationUser>, IQueryable<ApplicationUser>> Capture(RadzenDataFilter<ApplicationUser> filter)
  {
    var filters = (filter.Filters ?? []).Select(Clone).ToArray();
    Validate(filters);
    var source = Array.Empty<ApplicationUser>().AsQueryable();
    var expression = source.Where(filters, filter.LogicalFilterOperator, filter.FilterCaseSensitivity).Expression;
    return query => query.Provider.CreateQuery<ApplicationUser>(new ReplaceSource(source.Expression, query.Expression).Visit(expression)!);
  }

  private static CompositeFilterDescriptor Clone(CompositeFilterDescriptor filter) => new() {
    Property = filter.Property, FilterProperty = filter.FilterProperty, Type = filter.Type,
    FilterOperator = filter.FilterOperator, LogicalFilterOperator = filter.LogicalFilterOperator,
    FilterValue = filter.FilterValue is IEnumerable<string> values ? values.ToArray() : filter.FilterValue,
    Filters = filter.Filters?.Select(Clone).ToArray()
  };

  private static void Validate(IEnumerable<CompositeFilterDescriptor> filters)
  {
    foreach (var filter in filters) {
      if (!string.IsNullOrEmpty(filter.Property) && !Fields.Contains(filter.Property))
        throw new ArgumentException("Недопустимое поле фильтра.");
      if (filter.Filters != null) Validate(filter.Filters);
    }
  }

  public static IQueryable<ApplicationUser> Sort(IQueryable<ApplicationUser> query, string? orderBy)
  {
    if (string.IsNullOrWhiteSpace(orderBy)) return query.OrderBy(p => p.Id);
    var clauses = new List<string>();
    foreach (var clause in orderBy.Split(',')) {
      var parts = clause.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (parts.Length is < 1 or > 2 || !Fields.Contains(parts[0])) throw new ArgumentException("Недопустимая сортировка.");
      var direction = parts.Length == 2 ? parts[1].ToLowerInvariant() : "asc";
      if (direction is not ("asc" or "desc")) throw new ArgumentException("Недопустимая сортировка.");
      clauses.Add($"{parts[0]} {direction}");
    }
    clauses.Add("Id asc");
    return query.OrderBy(string.Join(", ", clauses));
  }

  private sealed class ReplaceSource(Expression original, Expression replacement) : ExpressionVisitor
  {
    public override Expression? Visit(Expression? node) => ReferenceEquals(node, original) ? replacement : base.Visit(node);
  }
}

