using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Notifications;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.Notifications;

public class NotificationService : INotificationService
{
    private readonly ApiClient _apiClient;

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
}
