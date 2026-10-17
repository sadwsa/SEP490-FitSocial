using System;
using System.Threading.Tasks;
using FitSocial.API.Hubs;
using FitSocial.Application.DTOs.Notifications;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace FitSocial.API.Services;

public class NotificationRealtimeNotifier : INotificationRealtimeNotifier
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationRealtimeNotifier(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendNotificationAsync(Guid recipientUserId, NotificationDto notification)
    {
        if (recipientUserId == Guid.Empty || notification == null)
        {
            return;
        }

        var title = notification.Type ?? "Notification";
        var message = notification.MessagePreview ?? string.Empty;

        // Support string (title, message) for frontend NotificationHubClient
        await _hubContext.Clients.Group($"User_{recipientUserId}").SendAsync("ReceiveNotification", title, message);
    }

    public async Task SendNotificationAsync(Guid recipientUserId, string title, string message)
    {
        if (recipientUserId == Guid.Empty)
        {
            return;
        }

        await _hubContext.Clients.Group($"User_{recipientUserId}").SendAsync("ReceiveNotification", title, message);
    }
}
