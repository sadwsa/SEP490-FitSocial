using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;

namespace FitSocial.Client.Services.Auth;

/// <summary>
/// Cầu nối Google Identity Services (nút Sign in with Google) với Blazor.
/// ClientId lấy từ wwwroot/appsettings.json (mục Google:ClientId).
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
    /// Vẽ nút Google vào thẻ div có id tương ứng. text: "signin_with" | "signup_with".
    /// Trả về true khi nút đã hiển thị, false khi không tải được GIS (mất mạng, bị chặn, cache cũ).
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
            // GIS load bất đồng bộ -> thử lại tối đa ~4 giây
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
            // fitSocialGoogle chưa tồn tại (index.html cũ bị cache) hoặc JS lỗi
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
