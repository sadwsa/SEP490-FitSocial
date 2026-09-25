using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Conversations;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Services;

public class ConversationService : IConversationService
{
    private readonly FitSocialDbContext _context;
    private readonly IChatRealtimeNotifier _realtimeNotifier;
    private readonly INotificationService _notificationService;

    public ConversationService(
        FitSocialDbContext context,
        IChatRealtimeNotifier realtimeNotifier,
        INotificationService notificationService)
    {
        _context = context;
        _realtimeNotifier = realtimeNotifier;
        _notificationService = notificationService;
    }

    public async Task<ApiResponseDto<List<ConversationDto>>> GetUserConversationsAsync(Guid userId)
    {
        try
        {
            // 1. Get participant records for this user
            var userParticipants = await _context.Participants
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .ToListAsync();

            if (!userParticipants.Any())
            {
                return ApiResponseDto<List<ConversationDto>>.Ok(new List<ConversationDto>(), "Conversations retrieved successfully.");
            }

            var conversationIds = userParticipants.Select(p => p.ConversationId).Distinct().ToList();
            var participantMap = userParticipants.ToDictionary(p => p.ConversationId);

            var blockedUserIds = await _context.UserBlocks
                .AsNoTracking()
                .Where(b => b.BlockerId == userId)
                .Select(b => b.BlockedId)
                .ToListAsync();

            // 2. Fetch conversations with participants (and user info) and messages
            var conversations = await _context.Conversations
                .AsNoTracking()
                .Where(c => conversationIds.Contains(c.Id) && c.IsDeleted != true)
                .Include(c => c.Participants)
                    .ThenInclude(p => p.User)
                .Include(c => c.Messages)
                .ToListAsync();

            var conversationDtos = new List<ConversationDto>();

            foreach (var conv in conversations)
            {
                if (!participantMap.TryGetValue(conv.Id, out var p))
                {
                    continue;
                }

                var dto = new ConversationDto
                {
                    ConversationId = conv.Id,
                    Type = conv.Type ?? "DIRECT",
                    UpdatedAt = conv.UpdatedAt ?? conv.CreatedAt
                };

                var otherParticipantForList = conv.Participants.FirstOrDefault(mp => mp.UserId != userId);
                bool isOtherBlocked = otherParticipantForList != null && blockedUserIds.Contains(otherParticipantForList.UserId);

                // Filter messages based on history deletion and block status
                var validMessages = conv.Messages
                    .Where(m => p.HistoryDeletedAt == null || (m.CreatedAt.HasValue && m.CreatedAt > p.HistoryDeletedAt))
                    .Where(m => !isOtherBlocked || (m.MessageType != "BLOCK" && m.MessageType != "AUTO" && m.MessageType != "BLOCKED" && m.MessageType != "AUTO_REPLY"))
                    .OrderByDescending(m => m.CreatedAt)
                    .ToList();

                // If user deleted history and there are no new messages after HistoryDeletedAt, do not show conversation
                if (p.HistoryDeletedAt != null && !validMessages.Any())
                {
                    continue;
                }

                var lastMessage = validMessages.FirstOrDefault();
                dto.LastMessage = lastMessage?.Content;
                dto.LastMessageAt = lastMessage?.CreatedAt ?? conv.UpdatedAt ?? conv.CreatedAt;
                dto.LastMessageSenderId = lastMessage?.SenderId;

                // Identify title and avatar
                if (string.Equals(conv.Type, "GROUP", StringComparison.OrdinalIgnoreCase))
                {
                    var memberNames = conv.Participants
                        .Where(mp => mp.User != null)
                        .Select(mp => mp.User.FullName ?? mp.User.Email)
                        .Take(3)
                        .ToList();

                    dto.Title = memberNames.Any() ? string.Join(", ", memberNames) : "Group Chat";
                    dto.DisplayAvatar = null;
                }
                else
                {
                    // DIRECT conversation: pick the other participant
                    var otherParticipant = conv.Participants.FirstOrDefault(mp => mp.UserId != userId);
                    var otherUser = otherParticipant?.User;

                    dto.OtherUserId = otherUser?.UserId;
                    dto.OtherUserName = otherUser?.FullName ?? otherUser?.Email ?? "FitSocial User";
                    dto.OtherUserAvatar = otherUser?.AvatarUrl;

                    dto.Title = dto.OtherUserName;
                    dto.DisplayAvatar = dto.OtherUserAvatar;
                }

                // Calculate Unread Count
                if (p.LastReadMessageId.HasValue)
                {
                    var lastReadMsg = conv.Messages.FirstOrDefault(m => m.Id == p.LastReadMessageId.Value);
                    if (lastReadMsg?.CreatedAt != null)
                    {
                        dto.UnreadCount = validMessages.Count(m => m.SenderId != userId && m.CreatedAt > lastReadMsg.CreatedAt);
                    }
                    else
                    {
                        dto.UnreadCount = validMessages.Count(m => m.SenderId != userId);
                    }
                }
                else
                {
                    dto.UnreadCount = validMessages.Count(m => m.SenderId != userId);
                }

                conversationDtos.Add(dto);
            }

            // Order conversations by newest message or activity first
            var sortedList = conversationDtos
                .OrderByDescending(c => c.LastMessageAt ?? c.UpdatedAt ?? DateTime.MinValue)
                .ToList();

            return ApiResponseDto<List<ConversationDto>>.Ok(sortedList, "Conversations retrieved successfully.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<List<ConversationDto>>.Fail($"System error retrieving conversations: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<ConversationDetailDto>> GetConversationDetailAsync(Guid conversationId, Guid currentUserId)
    {
        try
        {
            // 1. Fetch conversation with participants (and user profiles) and messages (and sender profiles)
            var conv = await _context.Conversations
                .AsNoTracking()
                .Include(c => c.Participants)
                    .ThenInclude(p => p.User)
                .Include(c => c.Messages)
                    .ThenInclude(m => m.Sender)
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.IsDeleted != true);

            if (conv == null)
            {
                return ApiResponseDto<ConversationDetailDto>.Fail("Conversation not found.");
            }

            // 2. Security / Authorization check: verify current authenticated user is a participant
            var currentParticipant = conv.Participants.FirstOrDefault(p => p.UserId == currentUserId);
            if (currentParticipant == null)
            {
                return ApiResponseDto<ConversationDetailDto>.Fail("Forbidden: You are not a participant in this conversation.");
            }

            var dto = new ConversationDetailDto
            {
                ConversationId = conv.Id,
                Type = conv.Type ?? "DIRECT",
                CreatedAt = conv.CreatedAt
            };

            // 3. Determine title, avatar, and other user info
            if (string.Equals(conv.Type, "GROUP", StringComparison.OrdinalIgnoreCase))
            {
                var memberNames = conv.Participants
                    .Where(mp => mp.User != null)
                    .Select(mp => mp.User.FullName ?? mp.User.Email)
                    .Take(3)
                    .ToList();

                dto.Title = memberNames.Any() ? string.Join(", ", memberNames) : "Group Chat";
                dto.DisplayAvatar = null;
            }
            else
            {
                var otherParticipant = conv.Participants.FirstOrDefault(mp => mp.UserId != currentUserId);
                var otherUser = otherParticipant?.User;

                dto.OtherUserId = otherUser?.UserId;
                dto.OtherUserName = otherUser?.FullName ?? otherUser?.Email ?? "FitSocial User";
                dto.OtherUserAvatar = otherUser?.AvatarUrl;

                dto.Title = dto.OtherUserName;
                dto.DisplayAvatar = dto.OtherUserAvatar;

                if (otherUser != null)
                {
                    dto.IsBlockedByMe = await _context.UserBlocks
                        .AnyAsync(b => b.BlockerId == currentUserId && b.BlockedId == otherUser.UserId);
                    dto.IsBlockedByOther = await _context.UserBlocks
                        .AnyAsync(b => b.BlockerId == otherUser.UserId && b.BlockedId == currentUserId);
                }
            }

            // 4. Filter messages based on history deletion date and block status
            var validMessages = conv.Messages
                .Where(m => currentParticipant.HistoryDeletedAt == null || (m.CreatedAt.HasValue && m.CreatedAt > currentParticipant.HistoryDeletedAt))
                .Where(m => !dto.IsBlockedByMe || (m.MessageType != "BLOCK" && m.MessageType != "AUTO" && m.MessageType != "BLOCKED" && m.MessageType != "AUTO_REPLY"))
                .OrderBy(m => m.CreatedAt ?? DateTime.MinValue)
                .ToList();

            // 5. Map to MessageDto with IsMine flag and chronological order
            dto.Messages = validMessages.Select(m => new MessageDto
            {
                Id = m.Id,
                ConversationId = m.ConversationId,
                SenderId = m.SenderId,
                SenderName = m.Sender?.FullName ?? m.Sender?.Email ?? "FitSocial User",
                SenderAvatar = m.Sender?.AvatarUrl,
                Content = m.Content,
                MessageType = m.MessageType,
                CreatedAt = m.CreatedAt,
                IsMine = (m.SenderId == currentUserId)
            }).ToList();

            // 6. Mark messages as read by updating LastReadMessageId
            var latestMessage = validMessages.LastOrDefault();
            if (latestMessage != null && currentParticipant.LastReadMessageId != latestMessage.Id)
            {
                var participantEntity = await _context.Participants
                    .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == currentUserId);
                if (participantEntity != null)
                {
                    participantEntity.LastReadMessageId = latestMessage.Id;
                    await _context.SaveChangesAsync();
                }
            }

            // 7. Mark unread message notifications for this conversation as read
            try
            {
                var messageIdsInConv = validMessages.Select(m => m.Id).ToList();
                if (messageIdsInConv.Any())
                {
                    var unreadConvNotifications = await _context.Notifications
                        .Where(n => n.UserId == currentUserId 
                                 && (n.IsRead == false || n.IsRead == null) 
                                 && n.ReferenceId.HasValue 
                                 && messageIdsInConv.Contains(n.ReferenceId.Value))
                        .ToListAsync();

                    if (unreadConvNotifications.Any())
                    {
                        foreach (var notif in unreadConvNotifications)
                        {
                            notif.IsRead = true;
                        }
                        await _context.SaveChangesAsync();
                    }
                }
            }
            catch
            {
                // Silently ignore notification mark error to not fail conversation detail
            }

            return ApiResponseDto<ConversationDetailDto>.Ok(dto, "Conversation detail retrieved successfully.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<ConversationDetailDto>.Fail($"System error retrieving conversation detail: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<MessageDto>> SendMessageAsync(Guid conversationId, Guid currentUserId, SendMessageRequestDto request)
    {
        try
        {
            // 1. Input Validation
            if (request == null || string.IsNullOrWhiteSpace(request.Content))
            {
                return ApiResponseDto<MessageDto>.Fail("Message content cannot be empty.");
            }

            var trimmedContent = request.Content.Trim();
            if (trimmedContent.Length > 300)
            {
                return ApiResponseDto<MessageDto>.Fail("Message content cannot exceed 300 characters.");
            }

            // 2. Validate Conversation Existence
            var conv = await _context.Conversations
                .Include(c => c.Participants)
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.IsDeleted != true);

            if (conv == null)
            {
                return ApiResponseDto<MessageDto>.Fail("Conversation not found.");
            }

            // 3. Authorization Check: Current user must be an active participant
            var isParticipant = conv.Participants.Any(p => p.UserId == currentUserId);
            if (!isParticipant)
            {
                return ApiResponseDto<MessageDto>.Fail("Forbidden: You are not a participant in this conversation.");
            }

            // Check blocking for DIRECT conversations
            if (!string.Equals(conv.Type, "GROUP", StringComparison.OrdinalIgnoreCase))
            {
                var otherParticipant = conv.Participants.FirstOrDefault(p => p.UserId != currentUserId);
                if (otherParticipant != null)
                {
                    var otherUserId = otherParticipant.UserId;

                    // If sender has blocked recipient, sender cannot send until unblocking
                    var isBlockedByMe = await _context.UserBlocks
                        .AnyAsync(b => b.BlockerId == currentUserId && b.BlockedId == otherUserId);
                    if (isBlockedByMe)
                    {
                        return ApiResponseDto<MessageDto>.Fail("You have blocked this user. Please unblock them before sending messages.");
                    }

                    // If recipient has blocked sender:
                    var isBlockedByOther = await _context.UserBlocks
                        .AnyAsync(b => b.BlockerId == otherUserId && b.BlockedId == currentUserId);

                    if (isBlockedByOther)
                    {
                        var now = DateTime.UtcNow;

                        // Save sender's message as BLOCK (VARCHAR(5))
                        var blockedMessage = new Message
                        {
                            Id = Guid.NewGuid(),
                            ConversationId = conversationId,
                            SenderId = currentUserId,
                            Content = trimmedContent,
                            MessageType = "BLOCK",
                            CreatedAt = now
                        };
                        _context.Messages.Add(blockedMessage);

                        // Generate automated response from recipient as AUTO (VARCHAR(5)), strictly after blockedMessage
                        var autoReplyMessage = new Message
                        {
                            Id = Guid.NewGuid(),
                            ConversationId = conversationId,
                            SenderId = otherUserId,
                            Content = "Sorry, I do not want to receive messages from you at the moment.",
                            MessageType = "AUTO",
                            CreatedAt = now.AddSeconds(1)
                        };
                        _context.Messages.Add(autoReplyMessage);

                        var senderPart = conv.Participants.FirstOrDefault(p => p.UserId == currentUserId);
                        if (senderPart != null)
                        {
                            senderPart.LastReadMessageId = autoReplyMessage.Id;
                        }
                        conv.UpdatedAt = now.AddSeconds(1);

                        await _context.SaveChangesAsync();

                        // Fetch sender and recipient profiles
                        var senderUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == currentUserId);
                        var recipientUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == otherUserId);

                        var autoReplyDto = new MessageDto
                        {
                            Id = autoReplyMessage.Id,
                            ConversationId = conversationId,
                            SenderId = otherUserId,
                            SenderName = recipientUser?.FullName ?? recipientUser?.Email ?? "FitSocial User",
                            SenderAvatar = recipientUser?.AvatarUrl,
                            Content = autoReplyMessage.Content,
                            MessageType = autoReplyMessage.MessageType,
                            CreatedAt = autoReplyMessage.CreatedAt,
                            IsMine = false
                        };

                        var senderMsgDto = new MessageDto
                        {
                            Id = blockedMessage.Id,
                            ConversationId = conversationId,
                            SenderId = currentUserId,
                            SenderName = senderUser?.FullName ?? senderUser?.Email ?? "FitSocial User",
                            SenderAvatar = senderUser?.AvatarUrl,
                            Content = blockedMessage.Content,
                            MessageType = blockedMessage.MessageType,
                            CreatedAt = blockedMessage.CreatedAt,
                            IsMine = true,
                            AutoReply = autoReplyDto
                        };

                        // Recipient has blocked sender: Recipient does not receive any message, broadcast, or notification.
                        // AutoReply is returned directly in senderMsgDto to ensure correct display order without WebSocket race conditions.
                        return ApiResponseDto<MessageDto>.Ok(senderMsgDto, "Message sent.");
                    }
                }
            }

            // 4. Normal Persistence: Create and save Message to database
            var message = new Message
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                SenderId = currentUserId,
                Content = trimmedContent,
                MessageType = "TEXT",
                CreatedAt = DateTime.UtcNow
            };

            _context.Messages.Add(message);
            conv.UpdatedAt = DateTime.UtcNow;

            var senderParticipant = conv.Participants.FirstOrDefault(p => p.UserId == currentUserId);
            if (senderParticipant != null)
            {
                senderParticipant.LastReadMessageId = message.Id;
            }

            await _context.SaveChangesAsync();

            // 5. Fetch sender details for accurate DTO mapping
            var sender = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == currentUserId);

            var broadcastDto = new MessageDto
            {
                Id = message.Id,
                ConversationId = message.ConversationId,
                SenderId = message.SenderId,
                SenderName = sender?.FullName ?? sender?.Email ?? "FitSocial User",
                SenderAvatar = sender?.AvatarUrl,
                Content = message.Content,
                MessageType = message.MessageType,
                CreatedAt = message.CreatedAt,
                IsMine = false // Determined by each client based on their authenticated userId
            };

            // 6. Broadcast message realtime through SignalR to all participants
            var participantUserIds = conv.Participants.Select(p => p.UserId).ToList();
            await _realtimeNotifier.BroadcastMessageAsync(participantUserIds, broadcastDto);

            // 6.1 UC-15: Send realtime notification to all participants except the sender
            var recipientUserIds = conv.Participants
                .Where(p => p.UserId != currentUserId)
                .Select(p => p.UserId)
                .ToList();

            if (recipientUserIds.Any())
            {
                try
                {
                    await _notificationService.CreateAndSendNewMessageNotificationAsync(message, sender, recipientUserIds);
                }
                catch
                {
                    // Failures in notification must not fail message delivery
                }
            }

            // 7. Return success response to sender
            var responseDto = new MessageDto
            {
                Id = broadcastDto.Id,
                ConversationId = broadcastDto.ConversationId,
                SenderId = broadcastDto.SenderId,
                SenderName = broadcastDto.SenderName,
                SenderAvatar = broadcastDto.SenderAvatar,
                Content = broadcastDto.Content,
                MessageType = broadcastDto.MessageType,
                CreatedAt = broadcastDto.CreatedAt,
                IsMine = true
            };

            return ApiResponseDto<MessageDto>.Ok(responseDto, "Message sent successfully.");
        }
        catch (Exception ex)
        {
            var detail = ex.InnerException != null ? $"{ex.Message} -> {ex.InnerException.Message}" : ex.Message;
            return ApiResponseDto<MessageDto>.Fail($"System error sending message: {detail}");
        }
    }

    public async Task<ApiResponseDto<bool>> DeleteConversationAsync(Guid conversationId, Guid currentUserId)
    {
        try
        {
            // 1. Validate Conversation Existence
            var conv = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.IsDeleted != true);

            if (conv == null)
            {
                return ApiResponseDto<bool>.Fail("Conversation not found.");
            }

            // 2. Authorization Check: Current user must be a participant
            var participant = await _context.Participants
                .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == currentUserId);

            if (participant == null)
            {
                return ApiResponseDto<bool>.Fail("Forbidden: You are not a participant in this conversation.");
            }

            // 3. Mark History Deleted At for the current user only
            participant.HistoryDeletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return ApiResponseDto<bool>.Ok(true, "Conversation deleted successfully.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<bool>.Fail($"System error deleting conversation: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<bool>> BlockUserAsync(Guid conversationId, Guid currentUserId)
    {
        try
        {
            var conv = await _context.Conversations
                .Include(c => c.Participants)
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.IsDeleted != true);

            if (conv == null)
            {
                return ApiResponseDto<bool>.Fail("Conversation not found.");
            }

            var isParticipant = conv.Participants.Any(p => p.UserId == currentUserId);
            if (!isParticipant)
            {
                return ApiResponseDto<bool>.Fail("Forbidden: You are not a participant in this conversation.");
            }

            if (string.Equals(conv.Type, "GROUP", StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponseDto<bool>.Fail("Blocking is only available for direct conversations.");
            }

            var otherParticipant = conv.Participants.FirstOrDefault(p => p.UserId != currentUserId);
            if (otherParticipant == null)
            {
                return ApiResponseDto<bool>.Fail("Other participant not found in conversation.");
            }

            var targetUserId = otherParticipant.UserId;
            var existingBlock = await _context.UserBlocks
                .FirstOrDefaultAsync(b => b.BlockerId == currentUserId && b.BlockedId == targetUserId);

            if (existingBlock == null)
            {
                var userBlock = new UserBlock
                {
                    Id = Guid.NewGuid(),
                    BlockerId = currentUserId,
                    BlockedId = targetUserId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.UserBlocks.Add(userBlock);
                await _context.SaveChangesAsync();
            }

            return ApiResponseDto<bool>.Ok(true, "User blocked successfully.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<bool>.Fail($"System error blocking user: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<bool>> UnblockUserAsync(Guid conversationId, Guid currentUserId)
    {
        try
        {
            var conv = await _context.Conversations
                .Include(c => c.Participants)
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.IsDeleted != true);

            if (conv == null)
            {
                return ApiResponseDto<bool>.Fail("Conversation not found.");
            }

            var isParticipant = conv.Participants.Any(p => p.UserId == currentUserId);
            if (!isParticipant)
            {
                return ApiResponseDto<bool>.Fail("Forbidden: You are not a participant in this conversation.");
            }

            var otherParticipant = conv.Participants.FirstOrDefault(p => p.UserId != currentUserId);
            if (otherParticipant == null)
            {
                return ApiResponseDto<bool>.Fail("Other participant not found in conversation.");
            }

            var targetUserId = otherParticipant.UserId;
            var existingBlock = await _context.UserBlocks
                .FirstOrDefaultAsync(b => b.BlockerId == currentUserId && b.BlockedId == targetUserId);

            if (existingBlock != null)
            {
                _context.UserBlocks.Remove(existingBlock);
                await _context.SaveChangesAsync();
            }

            return ApiResponseDto<bool>.Ok(true, "User unblocked successfully.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<bool>.Fail($"System error unblocking user: {ex.Message}");
        }
    }
}
