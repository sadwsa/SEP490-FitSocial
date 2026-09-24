using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Notifications;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.Notifications;

public class NotificationService : INotificationService
{
    private readonly ApiClient _apiClient;

    public event Action<Guid>? OnConversationRead;

    public NotificationService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResponse<List<NotificationDto>>> GetNotificationsAsync()
    {
        return await _apiClient.GetAsync<List<NotificationDto>>("notifications");
    }

    public async Task<ApiResponse<bool>> MarkAsReadAsync(Guid notificationId)
    {
        return await _apiClient.PutAsync<bool>($"notifications/{notificationId}/read");
    }

    public async Task<ApiResponse<bool>> MarkAllAsReadAsync()
    {
        return await _apiClient.PutAsync<bool>("notifications/read-all");
    }

    public void NotifyConversationRead(Guid conversationId)
    {
        OnConversationRead?.Invoke(conversationId);
    }

    public async Task MarkConversationNotificationsAsReadAsync(Guid conversationId)
    {
        NotifyConversationRead(conversationId);
        try
        {
            var response = await GetNotificationsAsync();
            if (response.Success && response.Data != null)
            {
                var unreadForConv = response.Data
                    .Where(n => n.ConversationId == conversationId && !n.IsRead)
                    .ToList();

                foreach (var notif in unreadForConv)
                {
                    await MarkAsReadAsync(notif.NotificationId);
                }
            }
        }
        catch { }
    }
}
