using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Notifications;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FitSocial.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly FitSocialDbContext _context;
    private readonly IChatRealtimeNotifier _realtimeNotifier;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        FitSocialDbContext context,
        IChatRealtimeNotifier realtimeNotifier,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _realtimeNotifier = realtimeNotifier;
        _logger = logger;
    }

    public async Task CreateAndSendNewMessageNotificationAsync(Message message, User sender, List<Guid> recipientUserIds)
    {
        if (message == null || recipientUserIds == null || !recipientUserIds.Any())
        {
            return;
        }

        try
        {
            var preview = message.Content;
            if (!string.IsNullOrEmpty(preview) && preview.Length > 100)
            {
                preview = preview.Substring(0, 97) + "...";
            }

            var senderName = sender?.FullName ?? sender?.Email ?? "FitSocial User";
            var senderAvatar = sender?.AvatarUrl;

            foreach (var recipientId in recipientUserIds)
            {
                // Never notify the sender of their own message
                if (recipientId == message.SenderId)
                {
                    continue;
                }

                // Idempotency: avoid creating duplicate notification for the same recipient + message + type
                var exists = await _context.Notifications
                    .AnyAsync(n => n.UserId == recipientId && n.ReferenceId == message.Id && n.Type == "NewMessage");

                Guid notificationId;
                DateTime createdAt = message.CreatedAt ?? DateTime.UtcNow;

                if (!exists)
                {
                    notificationId = Guid.NewGuid();
                    var notification = new Notification
                    {
                        Id = notificationId,
                        UserId = recipientId,
                        Type = "NewMessage",
                        ReferenceId = message.Id,
                        IsRead = false,
                        CreatedAt = createdAt
                    };

                    _context.Notifications.Add(notification);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    var existing = await _context.Notifications
                        .FirstOrDefaultAsync(n => n.UserId == recipientId && n.ReferenceId == message.Id && n.Type == "NewMessage");
                    notificationId = existing?.Id ?? Guid.NewGuid();
                    createdAt = existing?.CreatedAt ?? createdAt;
                }

                // Prepare DTO and send realtime notification via ChatHub group
                var dto = new NotificationDto
                {
                    NotificationId = notificationId,
                    Type = "NewMessage",
                    SenderId = message.SenderId,
                    SenderName = senderName,
                    SenderAvatar = senderAvatar,
                    ConversationId = message.ConversationId,
                    MessageId = message.Id,
                    MessagePreview = preview,
                    CreatedAt = createdAt,
                    IsRead = false
                };

                try
                {
                    await _realtimeNotifier.SendNotificationAsync(recipientId, dto);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send realtime notification to user {RecipientId}", recipientId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating and sending notifications for message {MessageId}", message.Id);
        }
    }

    public async Task<ApiResponseDto<List<NotificationDto>>> GetUserNotificationsAsync(Guid userId)
    {
        try
        {
            var notifications = await _context.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .ToListAsync();

            if (!notifications.Any())
            {
                return ApiResponseDto<List<NotificationDto>>.Ok(new List<NotificationDto>(), "Notifications retrieved successfully.");
            }

            var messageIds = notifications
                .Where(n => n.ReferenceId.HasValue && (n.Type == "NewMessage" || n.Type == "NEW_MESSAGE"))
                .Select(n => n.ReferenceId!.Value)
                .Distinct()
                .ToList();

            var messagesDict = new Dictionary<Guid, Message>();
            if (messageIds.Any())
            {
                messagesDict = await _context.Messages
                    .AsNoTracking()
                    .Include(m => m.Sender)
                    .Where(m => messageIds.Contains(m.Id))
                    .ToDictionaryAsync(m => m.Id);
            }

            var dtos = new List<NotificationDto>();
            foreach (var n in notifications)
            {
                var dto = new NotificationDto
                {
                    NotificationId = n.Id,
                    Type = n.Type ?? "NewMessage",
                    CreatedAt = n.CreatedAt,
                    IsRead = n.IsRead == true
                };

                if (n.ReferenceId.HasValue && messagesDict.TryGetValue(n.ReferenceId.Value, out var msg))
                {
                    dto.SenderId = msg.SenderId;
                    dto.SenderName = msg.Sender?.FullName ?? msg.Sender?.Email ?? "FitSocial User";
                    dto.SenderAvatar = msg.Sender?.AvatarUrl;
                    dto.ConversationId = msg.ConversationId;
                    dto.MessageId = msg.Id;

                    var preview = msg.Content;
                    if (!string.IsNullOrEmpty(preview) && preview.Length > 100)
                    {
                        preview = preview.Substring(0, 97) + "...";
                    }
                    dto.MessagePreview = preview;
                }

                dtos.Add(dto);
            }

            return ApiResponseDto<List<NotificationDto>>.Ok(dtos, "Notifications retrieved successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notifications for user {UserId}", userId);
            return ApiResponseDto<List<NotificationDto>>.Fail($"System error retrieving notifications: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<bool>> MarkAsReadAsync(Guid notificationId, Guid userId)
    {
        try
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null)
            {
                return ApiResponseDto<bool>.Fail("Notification not found.");
            }

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return ApiResponseDto<bool>.Ok(true, "Notification marked as read.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification {NotificationId} as read", notificationId);
            return ApiResponseDto<bool>.Fail($"System error marking notification as read: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<bool>> MarkAllAsReadAsync(Guid userId)
    {
        try
        {
            var unreadNotifications = await _context.Notifications
                .Where(n => n.UserId == userId && (n.IsRead == false || n.IsRead == null))
                .ToListAsync();

            if (unreadNotifications.Any())
            {
                foreach (var n in unreadNotifications)
                {
                    n.IsRead = true;
                }

                await _context.SaveChangesAsync();
            }

            return ApiResponseDto<bool>.Ok(true, "All notifications marked as read.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications as read for user {UserId}", userId);
            return ApiResponseDto<bool>.Fail($"System error marking all notifications as read: {ex.Message}");
        }
    }
}
