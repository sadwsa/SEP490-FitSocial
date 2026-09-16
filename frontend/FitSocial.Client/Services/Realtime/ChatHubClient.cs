using System;
using System.Threading.Tasks;
using FitSocial.Client.Models.Conversations;
using FitSocial.Client.Services.Auth;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;

namespace FitSocial.Client.Services.Realtime;

public class ChatHubClient : IChatHubClient
{
    private HubConnection? _hubConnection;
    private readonly IConfiguration _configuration;
    private readonly ITokenStorage _tokenStorage;

    public event Action<MessageDto>? OnMessageReceived;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public ChatHubClient(IConfiguration configuration, ITokenStorage tokenStorage)
    {
        _configuration = configuration;
        _tokenStorage = tokenStorage;
    }

    public async Task StartAsync(string? token = null)
    {
        if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
        {
            return;
        }

        if (string.IsNullOrEmpty(token))
        {
            token = await _tokenStorage.GetItemAsync<string>("authToken");
        }

        var hubUrl = _configuration["SignalR:ChatHubUrl"] ?? "https://localhost:7200/hubs/chat";

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

        _hubConnection.On<MessageDto>("ReceiveMessage", (message) =>
        {
            OnMessageReceived?.Invoke(message);
        });

        try
        {
            await _hubConnection.StartAsync();
        }
        catch
        {
            // Initial connection failure handled gracefully
        }
    }

    public async Task StopAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}
