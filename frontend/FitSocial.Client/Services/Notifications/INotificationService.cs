using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Notifications;

namespace FitSocial.Client.Services.Notifications;

public interface INotificationService
{
    Task<ApiResponse<List<NotificationDto>>> GetNotificationsAsync();
    Task<ApiResponse<bool>> MarkAsReadAsync(Guid notificationId);
    Task<ApiResponse<bool>> MarkAllAsReadAsync();

    event Action<Guid>? OnConversationRead;
    void NotifyConversationRead(Guid conversationId);
    Task MarkConversationNotificationsAsReadAsync(Guid conversationId);
}

