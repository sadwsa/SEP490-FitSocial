using System;
using System.Threading.Tasks;
using FitSocial.Client.Services.Auth;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FitSocial.Client.Services.Realtime;

public class PostHubClient : IPostHubClient
{
    private HubConnection? _hubConnection;
    private readonly IConfiguration _configuration;
    private readonly ITokenStorage _tokenStorage;
    private readonly ILogger<PostHubClient>? _logger;

    public event Action<Guid>? OnPostDeleted;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public PostHubClient(IConfiguration configuration, ITokenStorage tokenStorage, ILogger<PostHubClient>? logger = null)
    {
        _configuration = configuration;
        _tokenStorage = tokenStorage;
        _logger = logger;
    }

    public async Task StartAsync(string? token = null)
    {
        if (_hubConnection != null && 
            (_hubConnection.State == HubConnectionState.Connected || _hubConnection.State == HubConnectionState.Connecting))
        {
            return;
        }

        if (string.IsNullOrEmpty(token))
        {
            token = await _tokenStorage.GetItemAsync<string>("authToken");
        }

        var hubUrl = _configuration["SignalR:PostHubUrl"] ?? "https://localhost:7200/hubs/posts";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                if (!string.IsNullOrEmpty(token))
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                }
            })
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<Guid>("PostDeleted", (postId) =>
        {
            _logger?.LogInformation("[PostHubClient] Received PostDeleted event for PostId: {PostId}", postId);
            OnPostDeleted?.Invoke(postId);
        });

        _hubConnection.Closed += async (error) =>
        {
            if (error != null)
            {
                _logger?.LogWarning(error, "[PostHubClient] Connection closed with error");
            }
            await Task.CompletedTask;
        };

        _hubConnection.Reconnecting += (error) =>
        {
            _logger?.LogInformation("[PostHubClient] Reconnecting due to error: {Error}", error?.Message);
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += (connectionId) =>
        {
            _logger?.LogInformation("[PostHubClient] Reconnected with ConnectionId: {ConnectionId}", connectionId);
            return Task.CompletedTask;
        };

        try
        {
            await _hubConnection.StartAsync();
            _logger?.LogInformation("[PostHubClient] Connected to PostHub at {HubUrl}", hubUrl);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[PostHubClient] Failed to start connection to PostHub at {HubUrl}", hubUrl);
        }
    }

    public async Task StopAsync()
    {
        if (_hubConnection != null)
        {
            try
            {
                await _hubConnection.StopAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[PostHubClient] Error stopping connection");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            try
            {
                await _hubConnection.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[PostHubClient] Error disposing connection");
            }
        }
    }
}
