using Blazored.LocalStorage;
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
    private bool _rememberMe = true;
    private bool _initialized = false;

    public BrowserTokenStorage(ILocalStorageService localStorage, IJSRuntime jsRuntime)
    {
        _localStorage = localStorage;
        _jsRuntime = jsRuntime;
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
        catch
        {
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
        catch
        {
            // Non-fatal: preference simply won't persist across restarts.
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
        catch
        {
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
        catch
        {
            // If session storage is unavailable, fall back to local storage.
            await _localStorage.SetItemAsync(key, value);
        }
    }

    public async Task RemoveItemAsync(string key)
    {
        try
        {
            await _localStorage.RemoveItemAsync(key);
        }
        catch
        {
        }
        try
        {
            await _jsRuntime.InvokeVoidAsync("fitSocialStorage.sessionRemove", key);
        }
        catch
        {
        }
    }
}
