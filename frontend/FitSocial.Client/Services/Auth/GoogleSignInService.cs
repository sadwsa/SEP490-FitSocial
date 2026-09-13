using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;

namespace FitSocial.Client.Services.Auth;

/// <summary>
/// Bridge between Google Identity Services (Sign in with Google button) and Blazor.
/// ClientId is read from wwwroot/appsettings.json (Google:ClientId section).
/// </summary>
public class GoogleSignInService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IConfiguration _configuration;
    private DotNetObjectReference<GoogleSignInService>? _dotNetRef;

    public GoogleSignInService(IJSRuntime jsRuntime, IConfiguration configuration)
    {
        _jsRuntime = jsRuntime;
        _configuration = configuration;
    }

    public event Func<string, Task>? OnCredential;

    public string? ClientId => _configuration["Google:ClientId"];

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !ClientId.Contains("YOUR_GOOGLE_CLIENT_ID");

    /// <summary>
    /// Renders the Google button into the div with the given id. text: "signin_with" | "signup_with".
    /// Returns true when the button is displayed, false when GIS could not load (offline, blocked, stale cache).
    /// </summary>
    public async Task<bool> RenderButtonAsync(string elementId, string text = "signin_with")
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(ClientId))
        {
            return false;
        }

        _dotNetRef ??= DotNetObjectReference.Create(this);

        try
        {
            // GIS loads asynchronously -> retry for ~4 seconds
            for (int i = 0; i < 8; i++)
            {
                bool rendered = await _jsRuntime.InvokeAsync<bool>(
                    "fitSocialGoogle.renderButton", elementId, ClientId, text, _dotNetRef);
                if (rendered)
                {
                    return true;
                }
                await Task.Delay(500);
            }
        }
        catch
        {
            // fitSocialGoogle missing (stale cached index.html) or JS error
        }

        return false;
    }

    [JSInvokable]
    public async Task OnGoogleCredential(string credential)
    {
        if (OnCredential != null)
        {
            await OnCredential.Invoke(credential);
        }
    }

    public ValueTask DisposeAsync()
    {
        _dotNetRef?.Dispose();
        return ValueTask.CompletedTask;
    }
}
