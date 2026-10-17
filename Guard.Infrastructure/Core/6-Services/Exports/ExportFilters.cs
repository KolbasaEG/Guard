using System.Linq.Expressions;
using System.Text.Json;
using Radzen;

namespace Guard.Core.Services.Exports;

// Server adapter for the existing Radzen predicate semantics. UI capture is in Components/Library.
public static class ExportFilters
{
  public static IQueryable<T> Apply<T>(IQueryable<T> query, ExportFilter? filter, IReadOnlySet<string> fields)
  {
    if (filter == null) return query;
    var types = fields.ToDictionary(field => field, field => {
      var type = typeof(T);
      foreach (var segment in field.Split('.')) type = type.GetProperty(segment)?.PropertyType
        ?? throw new ArgumentException("Неизвестное поле фильтра.");
      return type;
    });
    ExportFilterValidation.Validate(filter, types);
    var parameter = Expression.Parameter(typeof(T), "row");
    Expression? Build(ExportFilter f) {
      if (f.Children != null) {
        Expression? group = null;
        foreach (var child in f.Children) {
          var condition = Build(child);
          if (condition == null) continue;
          group = group == null ? condition : f.All ? Expression.AndAlso(group, condition) : Expression.OrElse(group, condition);
        }
        return group;
      }
      if (string.IsNullOrWhiteSpace(f.Field)) return null;
      var operation = Enum.Parse<FilterOperator>(f.Operation!);
      var type = types[f.Field];
      var array = f.Value is { ValueKind: JsonValueKind.Array };
      var value = ExportFilterValidation.ReadValue(f.Value, type);
      // Preserve exact timestamp boundaries, including UTC ranges captured for a local calendar day.
      var underlying = Nullable.GetUnderlyingType(type) ?? type;
      if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset)) {
        Expression member = parameter;
        foreach (var segment in f.Field.Split('.')) member = Expression.Property(member, segment);
        if (operation is FilterOperator.IsNull or FilterOperator.IsNotNull) return underlying == type
          ? Expression.Constant(operation == FilterOperator.IsNotNull)
          : operation == FilterOperator.IsNull ? Expression.Equal(member, Expression.Constant(null, type))
            : Expression.NotEqual(member, Expression.Constant(null, type));
        if (value == null) return null;
        var constant = Expression.Convert(Expression.Constant(value, underlying), type);
        return operation switch {
          FilterOperator.Equals => Expression.Equal(member, constant), FilterOperator.NotEquals => Expression.NotEqual(member, constant),
          FilterOperator.GreaterThan => Expression.GreaterThan(member, constant), FilterOperator.GreaterThanOrEquals => Expression.GreaterThanOrEqual(member, constant),
          FilterOperator.LessThan => Expression.LessThan(member, constant), FilterOperator.LessThanOrEquals => Expression.LessThanOrEqual(member, constant),
          _ => throw new ArgumentException("Недопустимая операция фильтра даты.")
        };
      }
      var descriptor = new CompositeFilterDescriptor { Property = f.Field, Type = array ? typeof(IEnumerable<string>) : type,
        FilterOperator = operation, FilterValue = value };
      var source = Array.Empty<T>().AsQueryable();
      var expression = source.Where([descriptor], LogicalFilterOperator.And, FilterCaseSensitivity.CaseInsensitive).Expression;
      if (ReferenceEquals(expression, source.Expression)) return null;
      var lambda = (LambdaExpression)((UnaryExpression)((MethodCallExpression)expression).Arguments[1]).Operand;
      return new ReplaceSource(lambda.Parameters[0], parameter).Visit(lambda.Body);
    }
    var predicate = Build(filter);
    return predicate == null ? query : query.Where(Expression.Lambda<Func<T, bool>>(predicate, parameter));
  }
  private sealed class ReplaceSource(Expression original, Expression replacement) : ExpressionVisitor
  {
    public override Expression? Visit(Expression? node) => ReferenceEquals(node, original) ? replacement : base.Visit(node);
  }
}
