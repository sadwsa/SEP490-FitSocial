using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;

namespace FitSocial.Client.Services.Auth;

public class GoogleSignInService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IConfiguration _configuration;
    private readonly NavigationManager _navigationManager;

    public GoogleSignInService(
        IJSRuntime jsRuntime,
        IConfiguration configuration,
        NavigationManager navigationManager)
    {
        _jsRuntime = jsRuntime;
        _configuration = configuration;
        _navigationManager = navigationManager;
    }

    public string? ClientId => _configuration["Google:ClientId"];

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !ClientId.Contains("YOUR_GOOGLE_CLIENT_ID");

    public string CallbackUrl =>
        _navigationManager.BaseUri.TrimEnd('/') + "/login-callback";

    public async Task BeginRedirectSignInAsync(string? role = null)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(ClientId))
        {
            return;
        }

        await _jsRuntime.InvokeVoidAsync(
            "fitSocialGoogle.signInRedirect", ClientId, CallbackUrl, role);
    }
}
