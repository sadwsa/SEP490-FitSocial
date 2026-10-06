using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Blazored.LocalStorage;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Services.Auth;

namespace FitSocial.Client.Services.Http;

public class ApiClient
{
    private readonly HttpClient _http;
    private readonly ITokenStorage _localStorage;
    private readonly IAuthService _authService;
    private const string AuthTokenKey = "authToken";

    public ApiClient(HttpClient http, ITokenStorage localStorage, IAuthService authService)
    {
        _http = http;
        _localStorage = localStorage;
        _authService = authService;
    }

    /// <summary>
    /// Reads the backend error message without deserializing the whole
    /// ApiResponse&lt;T&gt;. Needed because e.g. ApiResponse&lt;bool&gt; fails
    /// to deserialize when the backend sends "data": null, which used to
    /// swallow real messages into a generic "API Error: ..." string.
    /// </summary>
    private static ApiResponse<T> ErrorFrom<T>(string errorJson, HttpStatusCode status)
    {
        try
        {
            using var doc = JsonDocument.Parse(errorJson);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                string? message = null;
                foreach (var name in new[] { "message", "Message" })
                {
                    if (root.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
                    {
                        message = prop.GetString();
                        if (!string.IsNullOrWhiteSpace(message)) break;
                        message = null;
                    }
                }

                var errors = new List<string>();
                foreach (var name in new[] { "errors", "Errors" })
                {
                    if (root.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in arr.EnumerateArray())
                        {
                            if (el.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(el.GetString()))
                            {
                                errors.Add(el.GetString()!);
                            }
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(message))
                {
                    return new ApiResponse<T> { Success = false, Message = message, Errors = errors };
                }
            }
        }
        catch { }

        return new ApiResponse<T> { Success = false, Message = $"API Error: {status}" };
    }

    private static ApiResponse ErrorFrom(string errorJson, HttpStatusCode status)
    {
        var typed = ErrorFrom<object>(errorJson, status);
        return new ApiResponse { Success = false, Message = typed.Message, Errors = typed.Errors };
    }

    private async Task AttachBearerTokenAsync()
    {
        var token = await _localStorage.GetItemAsync<string>(AuthTokenKey);
        if (!string.IsNullOrWhiteSpace(token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _http.DefaultRequestHeaders.Authorization = null;
        }
    }

    private static bool IsAuthEndpoint(string endpoint) =>
        endpoint.StartsWith("auth/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// On 401 tries one silent refresh (except for auth endpoints themselves),
    /// so short-lived access tokens never interrupt the user.
    /// </summary>
    private async Task<bool> TryRefreshOnceAsync(string endpoint, HttpStatusCode status)
    {
        if (status != HttpStatusCode.Unauthorized || IsAuthEndpoint(endpoint))
        {
            return false;
        }

        try
        {
            return await _authService.RefreshTokenAsync();
        }
        catch
        {
            return false;
        }
    }

    public async Task<ApiResponse<T>> GetAsync<T>(string endpoint)
    {
        try
        {
            await AttachBearerTokenAsync();
            var response = await _http.GetAsync(endpoint);
            if (response.StatusCode == HttpStatusCode.Unauthorized &&
                await TryRefreshOnceAsync(endpoint, response.StatusCode))
            {
                await AttachBearerTokenAsync();
                response = await _http.GetAsync(endpoint);
            }
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                try
                {
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<T>>(json, options);
                    if (apiResponse != null)
                    {
                        return apiResponse;
                    }
                }
                catch
                {
                    try
                    {
                        var data = JsonSerializer.Deserialize<T>(json, options);
                        return new ApiResponse<T> { Success = true, Data = data };
                    }
                    catch { }
                }

                return new ApiResponse<T> { Success = true };
            }

            try
            {
                var errorJson = await response.Content.ReadAsStringAsync();
                return ErrorFrom<T>(errorJson, response.StatusCode);
            }
            catch
            {
                return new ApiResponse<T>
                {
                    Success = false,
                    Message = $"API Error: {response.StatusCode}"
                };
            }
        }
        catch (Exception ex)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = $"Network Error: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<TResult>> PostAsync<TRequest, TResult>(string endpoint, TRequest payload)
    {
        try
        {
            await AttachBearerTokenAsync();
            var response = await _http.PostAsJsonAsync(endpoint, payload);
            if (response.StatusCode == HttpStatusCode.Unauthorized &&
                await TryRefreshOnceAsync(endpoint, response.StatusCode))
            {
                await AttachBearerTokenAsync();
                response = await _http.PostAsJsonAsync(endpoint, payload);
            }
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                try
                {
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<TResult>>(json, options);
                    if (apiResponse != null)
                    {
                        return apiResponse;
                    }
                }
                catch
                {
                    try
                    {
                        var data = JsonSerializer.Deserialize<TResult>(json, options);
                        return new ApiResponse<TResult> { Success = true, Data = data };
                    }
                    catch { }
                }

                return new ApiResponse<TResult> { Success = true };
            }

            try
            {
                var errorJson = await response.Content.ReadAsStringAsync();
                return ErrorFrom<TResult>(errorJson, response.StatusCode);
            }
            catch
            {
                return new ApiResponse<TResult>
                {
                    Success = false,
                    Message = $"API Error: {response.StatusCode}"
                };
            }
        }
        catch (Exception ex)
        {
            return new ApiResponse<TResult>
            {
                Success = false,
                Message = $"Network Error: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<TResult>> PostAsync<TResult>(string endpoint)
    {
        try
        {
            await AttachBearerTokenAsync();
            var response = await _http.PostAsync(endpoint, null);
            if (response.StatusCode == HttpStatusCode.Unauthorized &&
                await TryRefreshOnceAsync(endpoint, response.StatusCode))
            {
                await AttachBearerTokenAsync();
                response = await _http.PostAsync(endpoint, null);
            }
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                try
                {
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<TResult>>(json, options);
                    if (apiResponse != null)
                    {
                        return apiResponse;
                    }
                }
                catch
                {
                    try
                    {
                        var data = JsonSerializer.Deserialize<TResult>(json, options);
                        return new ApiResponse<TResult> { Success = true, Data = data };
                    }
                    catch { }
                }

                return new ApiResponse<TResult> { Success = true };
            }

            try
            {
                var errorJson = await response.Content.ReadAsStringAsync();
                return ErrorFrom<TResult>(errorJson, response.StatusCode);
            }
            catch
            {
                return new ApiResponse<TResult>
                {
                    Success = false,
                    Message = $"API Error: {response.StatusCode}"
                };
            }
        }
        catch (Exception ex)
        {
            return new ApiResponse<TResult>
            {
                Success = false,
                Message = $"Network Error: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<TResult>> PutAsync<TRequest, TResult>(string endpoint, TRequest payload)
    {
        try
        {
            await AttachBearerTokenAsync();
            var response = await _http.PutAsJsonAsync(endpoint, payload);
            if (response.StatusCode == HttpStatusCode.Unauthorized &&
                await TryRefreshOnceAsync(endpoint, response.StatusCode))
            {
                await AttachBearerTokenAsync();
                response = await _http.PutAsJsonAsync(endpoint, payload);
            }
            if (response.IsSuccessStatusCode)
            {
                var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<TResult>>();
                return apiResponse ?? new ApiResponse<TResult> { Success = true };
            }

            try
            {
                var errorJson = await response.Content.ReadAsStringAsync();
                return ErrorFrom<TResult>(errorJson, response.StatusCode);
            }
            catch
            {
                return new ApiResponse<TResult>
                {
                    Success = false,
                    Message = $"API Error: {response.StatusCode}"
                };
            }
        }
        catch (Exception ex)
        {
            return new ApiResponse<TResult>
            {
                Success = false,
                Message = $"Network Error: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<TResult>> DeleteAsync<TResult>(string endpoint)
    {
        try
        {
            await AttachBearerTokenAsync();
            var response = await _http.DeleteAsync(endpoint);
            if (response.StatusCode == HttpStatusCode.Unauthorized &&
                await TryRefreshOnceAsync(endpoint, response.StatusCode))
            {
                await AttachBearerTokenAsync();
                response = await _http.DeleteAsync(endpoint);
            }
            if (response.IsSuccessStatusCode)
            {
                var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<TResult>>();
                return apiResponse ?? new ApiResponse<TResult> { Success = true };
            }

            try
            {
                var errorJson = await response.Content.ReadAsStringAsync();
                return ErrorFrom<TResult>(errorJson, response.StatusCode);
            }
            catch
            {
                return new ApiResponse<TResult>
                {
                    Success = false,
                    Message = $"API Error: {response.StatusCode}"
                };
            }
        }
        catch (Exception ex)
        {
            return new ApiResponse<TResult>
            {
                Success = false,
                Message = $"Network Error: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<TResult>> PutAsync<TResult>(string endpoint)
    {
        try
        {
            await AttachBearerTokenAsync();
            var response = await _http.PutAsync(endpoint, null);
            if (response.StatusCode == HttpStatusCode.Unauthorized &&
                await TryRefreshOnceAsync(endpoint, response.StatusCode))
            {
                await AttachBearerTokenAsync();
                response = await _http.PutAsync(endpoint, null);
            }
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                try
                {
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<TResult>>(json, options);
                    if (apiResponse != null)
                    {
                        return apiResponse;
                    }
                }
                catch
                {
                    try
                    {
                        var data = JsonSerializer.Deserialize<TResult>(json, options);
                        return new ApiResponse<TResult> { Success = true, Data = data };
                    }
                    catch { }
                }
                
                return new ApiResponse<TResult> { Success = true };
            }

            try
            {
                var errorJson = await response.Content.ReadAsStringAsync();
                return ErrorFrom<TResult>(errorJson, response.StatusCode);
            }
            catch
            {
                return new ApiResponse<TResult>
                {
                    Success = false,
                    Message = $"API Error: {response.StatusCode}"
                };
            }
        }
        catch (Exception ex)
        {
            return new ApiResponse<TResult>
            {
                Success = false,
                Message = $"Network Error: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse> DeleteAsync(string endpoint)
    {
        try
        {
            await AttachBearerTokenAsync();
            var response = await _http.DeleteAsync(endpoint);
            if (response.StatusCode == HttpStatusCode.Unauthorized &&
                await TryRefreshOnceAsync(endpoint, response.StatusCode))
            {
                await AttachBearerTokenAsync();
                response = await _http.DeleteAsync(endpoint);
            }
            return new ApiResponse
            {
                Success = response.IsSuccessStatusCode,
                Message = response.IsSuccessStatusCode ? null : $"API Error: {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse
            {
                Success = false,
                Message = $"Network Error: {ex.Message}"
            };
        }
    }
}
