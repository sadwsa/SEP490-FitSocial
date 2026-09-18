using System;
using System.Threading.Tasks;
using FitSocial.Client.Models.Conversations;
using FitSocial.Client.Models.Notifications;

namespace FitSocial.Client.Services.Realtime;

public interface IChatHubClient : IAsyncDisposable
{
    bool IsConnected { get; }
    Task StartAsync(string? token = null);
    Task StopAsync();
    event Action<MessageDto>? OnMessageReceived;
    event Action<NotificationDto>? OnNotificationReceived;
}
