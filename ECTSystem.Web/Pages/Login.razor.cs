using ECTSystem.Web.Services;
using Microsoft.AspNetCore.Components;

namespace ECTSystem.Web.Pages;

public partial class Login
{
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private LoginFormModel formModel = new();
    private string errorMessage;

    private async Task OnSubmit()
    {
        errorMessage = null;

        var result = await AuthService.LoginAsync(formModel.Username, formModel.Password);

        if (result.Succeeded)
        {
            Navigation.NavigateTo("/", forceLoad: true);
        }
        else
        {
            errorMessage = result.Error ?? "Login failed. Please check your credentials.";
        }
    }

    private class LoginFormModel
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
