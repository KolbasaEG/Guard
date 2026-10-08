using Guard.Core.Entities;
using Guard.Core.Services;
using Microsoft.AspNetCore.Identity;

namespace Guard.Core.Identity;

public class PolicyPasswordValidator(IAccountPolicyService policies) : IPasswordValidator<ApplicationUser>
{
  public async Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
  {
    var p = await policies.GetAsync();
    return Validate(p, password);
  }

  public static IdentityResult Validate(AccountPolicy p, string? password)
  {
    var errors = new List<IdentityError>();
    var value = password ?? "";
    void Add(string code, string message) => errors.Add(new() { Code = code, Description = message });
    if (value.Length < p.MinimumLength || value.Length > 100) Add("PasswordLength", $"Пароль должен содержать от {p.MinimumLength} до 100 символов.");
    if (p.RequireDigit && !value.Any(c => c is >= '0' and <= '9')) Add("PasswordDigit", "Добавьте цифру (0–9).");
    if (p.RequireLowercase && !value.Any(c => c is >= 'a' and <= 'z')) Add("PasswordLower", "Добавьте строчную латинскую букву.");
    if (p.RequireUppercase && !value.Any(c => c is >= 'A' and <= 'Z')) Add("PasswordUpper", "Добавьте прописную латинскую букву.");
    if (p.RequireSymbol && !value.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c))) Add("PasswordSymbol", "Добавьте специальный символ.");
    if (value.Distinct().Count() < p.UniqueCharacters) Add("PasswordUnique", $"Добавьте не менее {p.UniqueCharacters} различных символов.");
    return errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed(errors.ToArray());
  }
}
