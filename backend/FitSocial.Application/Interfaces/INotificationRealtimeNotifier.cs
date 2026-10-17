using System;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Notifications;

namespace FitSocial.Application.Interfaces;

public interface INotificationRealtimeNotifier
{
    Task SendNotificationAsync(Guid recipientUserId, NotificationDto notification);
    Task SendNotificationAsync(Guid recipientUserId, string title, string message);
}
