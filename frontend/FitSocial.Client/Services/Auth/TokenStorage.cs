using Blazored.LocalStorage;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace FitSocial.Client.Services.Auth;

public interface ITokenStorage
{
    Task<bool> GetRememberMeAsync();
    Task SetRememberMeAsync(bool remember);
    Task<T?> GetItemAsync<T>(string key);
    Task SetItemAsync<T>(string key, T value);
    Task RemoveItemAsync(string key);
}

/// <summary>
/// Token store honoring "Remember me": localStorage when remembered,
/// sessionStorage (cleared when the tab/browser closes) otherwise.
/// </summary>
public class BrowserTokenStorage : ITokenStorage
{
    private const string RememberKey = "rememberMe";
    private readonly ILocalStorageService _localStorage;
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<BrowserTokenStorage> _logger;
    private bool _rememberMe = true;
    private bool _initialized = false;

    public BrowserTokenStorage(
        ILocalStorageService localStorage,
        IJSRuntime jsRuntime,
        ILogger<BrowserTokenStorage> logger)
    {
        _localStorage = localStorage;
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized)
        {
            return;
        }
        _initialized = true;
        try
        {
            var saved = await _localStorage.GetItemAsync<string>(RememberKey);
            _rememberMe = saved != "0";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read the remember-me preference. Defaulting to local storage.");
            _rememberMe = true;
        }
    }

    public async Task<bool> GetRememberMeAsync()
    {
        await EnsureInitializedAsync();
        return _rememberMe;
    }

    public async Task SetRememberMeAsync(bool remember)
    {
        _rememberMe = remember;
        try
        {
            await _localStorage.SetItemAsync(RememberKey, remember ? "1" : "0");
        }
        catch (Exception ex)
        {
            // Non-fatal: preference simply won't persist across restarts.
            _logger.LogWarning(ex, "Could not persist the remember-me preference.");
        }
    }

    public async Task<T?> GetItemAsync<T>(string key)
    {
        await EnsureInitializedAsync();
        if (_rememberMe)
        {
            return await _localStorage.GetItemAsync<T>(key);
        }
        try
        {
            return await _jsRuntime.InvokeAsync<T?>("fitSocialStorage.sessionGet", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read '{Key}' from session storage.", key);
            return default;
        }
    }

    public async Task SetItemAsync<T>(string key, T value)
    {
        await EnsureInitializedAsync();
        if (_rememberMe)
        {
            await _localStorage.SetItemAsync(key, value);
            return;
        }
        try
        {
            await _jsRuntime.InvokeVoidAsync("fitSocialStorage.sessionSet", key, value);
        }
        catch (Exception ex)
        {
            // If session storage is unavailable, fall back to local storage.
            _logger.LogWarning(ex, "Session storage write failed for '{Key}'. Falling back to local storage.", key);
            await _localStorage.SetItemAsync(key, value);
        }
    }

    public async Task RemoveItemAsync(string key)
    {
        try
        {
            await _localStorage.RemoveItemAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not remove '{Key}' from local storage.", key);
        }
        try
        {
            await _jsRuntime.InvokeVoidAsync("fitSocialStorage.sessionRemove", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not remove '{Key}' from session storage.", key);
        }
    }
}
