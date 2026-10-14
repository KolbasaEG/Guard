using Microsoft.AspNetCore.DataProtection;

namespace Guard.Core.Services;

public sealed class ApiCredentialProtector(IDataProtectionProvider provider)
{
  private readonly IDataProtector protector = provider.CreateProtector("Guard.DirectoryApi.BasicPassword.v1");
  public string Protect(string password) => protector.Protect(password);
  public string Unprotect(string password)
  {
    try { return protector.Unprotect(password); }
    catch (System.Security.Cryptography.CryptographicException)
    { throw new EntityRuleException("Не удалось прочитать пароль API. Root должен сохранить пароль повторно."); }
  }
}
