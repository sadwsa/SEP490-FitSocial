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
    public event Action? OnNotificationsChanged;

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
        var res = await _apiClient.PutAsync<bool>($"notifications/{notificationId}/read");
        if (res.Success)
        {
            NotifyNotificationsChanged();
        }
        return res;
    }

    public async Task<ApiResponse<bool>> MarkAllAsReadAsync()
    {
        var res = await _apiClient.PutAsync<bool>("notifications/read-all");
        if (res.Success)
        {
            NotifyNotificationsChanged();
        }
        return res;
    }

    public void NotifyConversationRead(Guid conversationId)
    {
        OnConversationRead?.Invoke(conversationId);
        NotifyNotificationsChanged();
    }

    public void NotifyNotificationsChanged()
    {
        OnNotificationsChanged?.Invoke();
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
                    await _apiClient.PutAsync<bool>($"notifications/{notif.NotificationId}/read");
                }
            }
        }
        catch { }
        finally
        {
            NotifyNotificationsChanged();
        }
    }
}
