using ECTSystem.Web.Services;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace ECTSystem.Web.Pages;

public partial class Login
{
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private string username = string.Empty;
    private string password = string.Empty;
    private string errorMessage;

    private async Task OnLogin(LoginArgs args)
    {
        errorMessage = null;

        var result = await AuthService.LoginAsync(args.Username, args.Password);

        if (result.Succeeded)
        {
            Navigation.NavigateTo("/", forceLoad: true);
        }
        else
        {
            errorMessage = result.Error ?? "Login failed. Please check your credentials.";
        }
    }

    private void OnRegister()
    {
        Navigation.NavigateTo("/register");
    }
}
