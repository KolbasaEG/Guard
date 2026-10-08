namespace Guard.Core.Services;
public sealed class EntityRuleException(string message) : InvalidOperationException(message);
