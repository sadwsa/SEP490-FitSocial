using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Conversations;
using FitSocial.Application.DTOs.Notifications;

namespace FitSocial.Application.Interfaces;

public interface IChatRealtimeNotifier
{
    Task BroadcastMessageAsync(List<Guid> participantUserIds, MessageDto message);
    Task SendNotificationAsync(Guid recipientUserId, NotificationDto notification);
}
