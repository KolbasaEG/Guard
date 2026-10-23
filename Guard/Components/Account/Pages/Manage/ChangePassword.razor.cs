using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;

namespace Guard.Components.Account.Pages.Manage;

public partial class ChangePassword
{
    [Inject] private IOwnPasswordService Passwords { get; set; } = default!;
    [Inject] private IAccountPolicyService Policies { get; set; } = default!;
    [Inject] private IdentityRedirectManager RedirectManager { get; set; } = default!;
    [Inject] private ILogger<ChangePassword> Logger { get; set; } = default!;
    [CascadingParameter] private HttpContext HttpContext { get; set; } = default!;
    [SupplyParameterFromForm] private InputModel Input { get; set; } = default!;
    [SupplyParameterFromQuery] private string? ReturnUrl { get; set; }
    private string? message;
    private bool canChange;
    private bool signInRequired;
    private bool mandatory;
    private string ScreenTitle => mandatory ? "Обязательная смена пароля" : "Смена пароля";
    private string SafeReturnUrl => AccountReturnUrl.Normalize(ReturnUrl);

    protected override async Task OnInitializedAsync()
    {
        Input ??= new();
        var state = await Passwords.GetStateAsync(HttpContext.RequestAborted);
        var userId = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (state == OwnPasswordState.MissingUser || string.IsNullOrEmpty(userId))
        {
            RedirectManager.RedirectToWithStatus("Account/InvalidUser", "Ошибка: Не удалось загрузить учётную запись. Войдите повторно.", HttpContext);
            return;
        }
        var policy = await Policies.CheckAsync(userId, HttpContext.RequestAborted);
        mandatory = policy.PasswordExpired;
        canChange = state == OwnPasswordState.Ready && !policy.Blocked;
        if (!canChange)
            message = policy.Blocked ? "Учётная запись заблокирована. Обратитесь к администратору."
                : "Ошибка: Для учётной записи не установлен локальный пароль. Обратитесь к администратору.";
    }

    private void ClearPasswords()
    {
        Input.OldPassword = "";
        Input.NewPassword = "";
        Input.ConfirmPassword = "";
    }

    private async Task OnValidSubmitAsync()
    {
        if (!canChange || signInRequired) return;
        OwnPasswordChangeResult result;
        try {
            result = await Passwords.ChangeAsync(Input.OldPassword, Input.NewPassword,
                Input.ConfirmPassword, HttpContext.RequestAborted);
        }
        finally { ClearPasswords(); }
        if (result.Status == OwnPasswordChangeStatus.Unauthorized)
        {
            canChange = false;
            RedirectManager.RedirectToWithStatus("Account/InvalidUser", "Ошибка: Не удалось загрузить учётную запись. Войдите повторно.", HttpContext);
            return;
        }
        if (result.Status == OwnPasswordChangeStatus.SignInRequired)
        {
            canChange = false;
            signInRequired = true;
            message = OwnPasswordChangeResult.SignInRequiredMessage;
            return;
        }
        if (result.Status != OwnPasswordChangeStatus.Succeeded)
        {
            message = $"Ошибка: {result.Error}";
            return;
        }
        Logger.LogInformation("User changed their password successfully.");
        RedirectManager.RedirectTo(mandatory ? "/" : SafeReturnUrl);
    }

    private sealed class InputModel
    {
        [Required(ErrorMessage = "Введите текущий пароль.")]
        [DataType(DataType.Password)]
        public string OldPassword { get; set; } = "";
        [Required(ErrorMessage = "Введите новый пароль.")]
        [StringLength(100, ErrorMessage = "Пароль должен содержать не более 100 символов.")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = "";
        [Required(ErrorMessage = "Повторите новый пароль.")]
        [Compare(nameof(NewPassword), ErrorMessage = "Пароли не совпадают.")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = "";
    }
}
