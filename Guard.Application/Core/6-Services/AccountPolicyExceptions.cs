namespace Guard.Core.Services;

public sealed class AccountPolicyConflictException() : InvalidOperationException("Настройки изменены в другой сессии. Обновите страницу.");
public sealed class UserSecurityException(string message) : InvalidOperationException(message);
