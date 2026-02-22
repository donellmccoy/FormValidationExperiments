using ECTSystem.Web.Services;
using Microsoft.AspNetCore.Components;

namespace ECTSystem.Web.Pages;

public partial class Register
{
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private RegisterFormModel formModel = new();
    private string errorMessage;

    private async Task OnRegister()
    {
        errorMessage = null;

        if (formModel.Password != formModel.ConfirmPassword)
        {
            errorMessage = "Passwords do not match.";
            return;
        }

        var result = await AuthService.RegisterAsync(formModel.Email, formModel.Password);

        if (result.Succeeded)
        {
            // Auto-login after successful registration
            var loginResult = await AuthService.LoginAsync(formModel.Email, formModel.Password);

            if (loginResult.Succeeded)
            {
                Navigation.NavigateTo("/", forceLoad: true);
            }
            else
            {
                Navigation.NavigateTo("/login");
            }
        }
        else
        {
            errorMessage = result.Error ?? "Registration failed. Please try again.";
        }
    }

    private sealed class RegisterFormModel
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
