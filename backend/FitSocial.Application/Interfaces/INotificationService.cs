using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Notifications;
using FitSocial.Domain.Entities;

namespace FitSocial.Application.Interfaces;

public interface INotificationService
{
    Task<ApiResponseDto<List<NotificationDto>>> GetUserNotificationsAsync(Guid userId);
    Task<ApiResponseDto<bool>> MarkAsReadAsync(Guid notificationId, Guid userId);
    Task<ApiResponseDto<bool>> MarkAllAsReadAsync(Guid userId);
    Task CreateAndSendNewMessageNotificationAsync(Message message, User sender, List<Guid> recipientUserIds);
}
