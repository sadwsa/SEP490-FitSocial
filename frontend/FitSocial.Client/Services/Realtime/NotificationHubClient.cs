using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;

namespace FitSocial.Client.Services.Realtime;

public interface INotificationHubClient : IAsyncDisposable
{
    bool IsConnected { get; }
    Task StartAsync(string? token = null);
    Task StopAsync();
    event Action<string, string>? OnNotificationReceived;
}

public class NotificationHubClient : INotificationHubClient
{
    private HubConnection? _hubConnection;
    private readonly IConfiguration _configuration;

    public event Action<string, string>? OnNotificationReceived;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public NotificationHubClient(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task StartAsync(string? token = null)
    {
        if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
        {
            return;
        }

        var hubUrl = _configuration["SignalR:NotificationHubUrl"] ?? "https://localhost:7200/hubs/notifications";

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

        _hubConnection.On<string, string>("ReceiveNotification", (title, message) =>
        {
            OnNotificationReceived?.Invoke(title, message);
        });

        try
        {
            await _hubConnection.StartAsync();
        }
        catch
        {
            // Log or handle initial connection failure gracefully
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
