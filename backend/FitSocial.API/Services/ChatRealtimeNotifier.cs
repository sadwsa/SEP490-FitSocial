using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FitSocial.API.Hubs;
using FitSocial.Application.DTOs.Conversations;
using FitSocial.Application.DTOs.Notifications;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace FitSocial.API.Services;

public class ChatRealtimeNotifier : IChatRealtimeNotifier
{
    private readonly IHubContext<ChatHub> _chatHubContext;
    private readonly IHubContext<NotificationHub> _notificationHubContext;

    public ChatRealtimeNotifier(
        IHubContext<ChatHub> chatHubContext,
        IHubContext<NotificationHub> notificationHubContext)
    {
        _chatHubContext = chatHubContext;
        _notificationHubContext = notificationHubContext;
    }

    public async Task BroadcastMessageAsync(List<Guid> participantUserIds, MessageDto message)
    {
        if (participantUserIds == null || !participantUserIds.Any())
        {
            return;
        }

        var groupNames = participantUserIds.Select(id => $"User_{id}").ToList();
        await _chatHubContext.Clients.Groups(groupNames).SendAsync("ReceiveMessage", message);
    }

    public async Task SendNotificationAsync(Guid recipientUserId, NotificationDto notification)
    {
        if (recipientUserId == Guid.Empty || notification == null)
        {
            return;
        }

        var isChatMessage = string.Equals(notification.Type, "NewMessage", StringComparison.OrdinalIgnoreCase);

        if (isChatMessage)
        {
            // Message events go to ChatHub
            await _chatHubContext.Clients.Group($"User_{recipientUserId}").SendAsync("ReceiveNotification", notification);
        }
        else
        {
            // Application notifications go to NotificationHub, NEVER to ChatHub
            var title = notification.Type ?? "Notification";
            var message = notification.MessagePreview ?? string.Empty;
            await _notificationHubContext.Clients.Group($"User_{recipientUserId}").SendAsync("ReceiveNotification", title, message);
        }
    }
}
