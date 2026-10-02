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
            // 1. Fetch all conversations where user is User1 or User2
            var conversations = await _context.Conversations
                .AsNoTracking()
                .Where(c => c.User1Id == userId || c.User2Id == userId)
                .Include(c => c.User1)
                .Include(c => c.User2)
                .Include(c => c.Messages)
                .ToListAsync();

            if (!conversations.Any())
            {
                return ApiResponseDto<List<ConversationDto>>.Ok(new List<ConversationDto>(), "Conversations retrieved successfully.");
            }

            var blockedUserIds = await _context.UserBlocks
                .AsNoTracking()
                .Where(b => b.BlockerId == userId)
                .Select(b => b.BlockedId)
                .ToListAsync();

            var conversationDtos = new List<ConversationDto>();

            foreach (var conv in conversations)
            {
                bool isUser1 = conv.User1Id == userId;
                var deletedHistoryAt = isUser1 ? conv.User1DeletedHistoryAt : conv.User2DeletedHistoryAt;
                var lastReadMessageId = isUser1 ? conv.User1LastReadMessageId : conv.User2LastReadMessageId;
                var otherUser = isUser1 ? conv.User2 : conv.User1;

                if (otherUser == null)
                {
                    continue;
                }

                // If user deleted history and no new message was posted after deletion, hide from active list
                if (deletedHistoryAt != null && conv.LastMessageAt != null && conv.LastMessageAt <= deletedHistoryAt)
                {
                    continue;
                }

                bool isOtherBlocked = blockedUserIds.Contains(otherUser.UserId);

                // Filter valid messages for unread calculation
                var validMessages = conv.Messages
                    .Where(m => deletedHistoryAt == null || (m.CreatedAt.HasValue && m.CreatedAt > deletedHistoryAt))
                    .Where(m => !isOtherBlocked || (m.MessageType != "BLOCK" && m.MessageType != "AUTO" && m.MessageType != "BLOCKED" && m.MessageType != "AUTO_REPLY"))
                    .OrderByDescending(m => m.CreatedAt)
                    .ToList();

                // If user deleted history and there are no messages after deletion, hide
                if (deletedHistoryAt != null && !validMessages.Any())
                {
                    continue;
                }

                var otherUserName = otherUser.FullName ?? otherUser.Email ?? "FitSocial User";

                var dto = new ConversationDto
                {
                    ConversationId = conv.ConversationId,
                    Type = "DIRECT",
                    Title = otherUserName,
                    DisplayAvatar = otherUser.AvatarUrl,
                    OtherUserId = otherUser.UserId,
                    OtherUserName = otherUserName,
                    OtherUserAvatar = otherUser.AvatarUrl,
                    LastMessage = conv.LastMessageContent,
                    LastMessageAt = conv.LastMessageAt ?? conv.UpdatedAt ?? conv.CreatedAt,
                    LastMessageSenderId = conv.LastMessageSenderId,
                    UpdatedAt = conv.UpdatedAt ?? conv.CreatedAt
                };

                // Calculate unread count
                if (lastReadMessageId.HasValue)
                {
                    var lastReadMsg = conv.Messages.FirstOrDefault(m => m.MessageId == lastReadMessageId.Value);
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

            // Order by most recent message or activity first
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
            var conv = await _context.Conversations
                .Include(c => c.User1)
                .Include(c => c.User2)
                .Include(c => c.Messages)
                    .ThenInclude(m => m.Sender)
                .Include(c => c.Messages)
                    .ThenInclude(m => m.MessageAttachments)
                .FirstOrDefaultAsync(c => c.ConversationId == conversationId);

            if (conv == null)
            {
                return ApiResponseDto<ConversationDetailDto>.Fail("Conversation not found.");
            }

            // Authorization check
            if (conv.User1Id != currentUserId && conv.User2Id != currentUserId)
            {
                return ApiResponseDto<ConversationDetailDto>.Fail("Forbidden: You are not a participant in this conversation.");
            }

            bool isUser1 = conv.User1Id == currentUserId;
            var otherUser = isUser1 ? conv.User2 : conv.User1;
            var deletedHistoryAt = isUser1 ? conv.User1DeletedHistoryAt : conv.User2DeletedHistoryAt;

            var otherUserName = otherUser?.FullName ?? otherUser?.Email ?? "FitSocial User";

            var dto = new ConversationDetailDto
            {
                ConversationId = conv.ConversationId,
                Type = "DIRECT",
                Title = otherUserName,
                DisplayAvatar = otherUser?.AvatarUrl,
                OtherUserId = otherUser?.UserId,
                OtherUserName = otherUserName,
                OtherUserAvatar = otherUser?.AvatarUrl,
                CreatedAt = conv.CreatedAt
            };

            if (otherUser != null)
            {
                dto.IsBlockedByMe = await _context.UserBlocks
                    .AnyAsync(b => b.BlockerId == currentUserId && b.BlockedId == otherUser.UserId);
                dto.IsBlockedByOther = await _context.UserBlocks
                    .AnyAsync(b => b.BlockerId == otherUser.UserId && b.BlockedId == currentUserId);
            }

            // Filter messages based on history deletion date
            var validMessages = conv.Messages
                .Where(m => deletedHistoryAt == null || (m.CreatedAt.HasValue && m.CreatedAt > deletedHistoryAt))
                .Where(m => !dto.IsBlockedByMe || (m.MessageType != "BLOCK" && m.MessageType != "AUTO" && m.MessageType != "BLOCKED" && m.MessageType != "AUTO_REPLY"))
                .OrderBy(m => m.CreatedAt ?? DateTime.MinValue)
                .ToList();

            dto.Messages = validMessages.Select(m => new MessageDto
            {
                Id = m.MessageId,
                ConversationId = m.ConversationId,
                SenderId = m.SenderId,
                SenderName = m.Sender?.FullName ?? m.Sender?.Email ?? "FitSocial User",
                SenderAvatar = m.Sender?.AvatarUrl,
                Content = m.Content,
                MessageType = m.MessageType,
                CreatedAt = m.CreatedAt,
                IsMine = (m.SenderId == currentUserId),
                Attachments = m.MessageAttachments.Select(a => new MessageAttachmentDto
                {
                    AttachmentId = a.AttachmentId,
                    MediaUrl = a.MediaUrl,
                    ThumbnailUrl = a.ThumbnailUrl,
                    MediaType = a.MediaType,
                    FileSize = a.FileSize,
                    DurationSeconds = a.DurationSeconds,
                    Width = a.Width,
                    Height = a.Height
                }).ToList()
            }).ToList();

            // Mark messages as read
            var latestMessage = validMessages.LastOrDefault();
            if (latestMessage != null)
            {
                if (isUser1)
                {
                    if (conv.User1LastReadMessageId != latestMessage.MessageId)
                    {
                        conv.User1LastReadMessageId = latestMessage.MessageId;
                        await _context.SaveChangesAsync();
                    }
                }
                else
                {
                    if (conv.User2LastReadMessageId != latestMessage.MessageId)
                    {
                        conv.User2LastReadMessageId = latestMessage.MessageId;
                        await _context.SaveChangesAsync();
                    }
                }
            }

            // Mark notifications as read
            try
            {
                var messageIds = validMessages.Select(m => m.MessageId).ToList();
                if (messageIds.Any())
                {
                    var unreadConvNotifications = await _context.Notifications
                        .Where(n => n.UserId == currentUserId
                                 && (n.IsRead == false || n.IsRead == null)
                                 && n.ReferenceId.HasValue
                                 && messageIds.Contains(n.ReferenceId.Value))
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
                // Silently ignore notification mark error
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
            if (request == null || (string.IsNullOrWhiteSpace(request.Content) && (request.Attachments == null || !request.Attachments.Any())))
            {
                return ApiResponseDto<MessageDto>.Fail("Message content or attachments cannot be empty.");
            }

            var trimmedContent = request.Content?.Trim();
            if (!string.IsNullOrEmpty(trimmedContent) && trimmedContent.Length > 1000)
            {
                return ApiResponseDto<MessageDto>.Fail("Message content cannot exceed 1000 characters.");
            }

            var conv = await _context.Conversations
                .FirstOrDefaultAsync(c => c.ConversationId == conversationId);

            if (conv == null)
            {
                return ApiResponseDto<MessageDto>.Fail("Conversation not found.");
            }

            if (conv.User1Id != currentUserId && conv.User2Id != currentUserId)
            {
                return ApiResponseDto<MessageDto>.Fail("Forbidden: You are not a participant in this conversation.");
            }

            bool isUser1 = conv.User1Id == currentUserId;
            var otherUserId = (isUser1 ? conv.User2Id : conv.User1Id) ?? Guid.Empty;

            // Check blocking
            var isBlockedByMe = await _context.UserBlocks
                .AnyAsync(b => b.BlockerId == currentUserId && b.BlockedId == otherUserId);
            if (isBlockedByMe)
            {
                return ApiResponseDto<MessageDto>.Fail("You have blocked this user. Please unblock them before sending messages.");
            }

            var isBlockedByOther = await _context.UserBlocks
                .AnyAsync(b => b.BlockerId == otherUserId && b.BlockedId == currentUserId);

            var now = DateTime.UtcNow;

            if (isBlockedByOther)
            {
                var blockedMessage = new Message
                {
                    MessageId = Guid.NewGuid(),
                    ConversationId = conversationId,
                    SenderId = currentUserId,
                    Content = trimmedContent,
                    MessageType = "BLOCK",
                    CreatedAt = now
                };
                _context.Messages.Add(blockedMessage);

                var autoReplyMessage = new Message
                {
                    MessageId = Guid.NewGuid(),
                    ConversationId = conversationId,
                    SenderId = otherUserId,
                    Content = "Sorry, I do not want to receive messages from you at the moment.",
                    MessageType = "AUTO",
                    CreatedAt = now.AddSeconds(1)
                };
                _context.Messages.Add(autoReplyMessage);

                conv.LastMessageContent = autoReplyMessage.Content;
                conv.LastMessageSenderId = otherUserId;
                conv.LastMessageAt = autoReplyMessage.CreatedAt;
                conv.UpdatedAt = autoReplyMessage.CreatedAt;

                if (isUser1)
                {
                    conv.User1LastReadMessageId = autoReplyMessage.MessageId;
                }
                else
                {
                    conv.User2LastReadMessageId = autoReplyMessage.MessageId;
                }

                await _context.SaveChangesAsync();

                var senderUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == currentUserId);
                var recipientUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == otherUserId);

                var autoReplyDto = new MessageDto
                {
                    Id = autoReplyMessage.MessageId,
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
                    Id = blockedMessage.MessageId,
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

                return ApiResponseDto<MessageDto>.Ok(senderMsgDto, "Message sent.");
            }

            // Normal Message Creation
            var message = new Message
            {
                MessageId = Guid.NewGuid(),
                ConversationId = conversationId,
                SenderId = currentUserId,
                Content = trimmedContent,
                MessageType = request.MessageType ?? "TEXT",
                CreatedAt = now
            };

            if (request.Attachments != null && request.Attachments.Any())
            {
                foreach (var att in request.Attachments)
                {
                    message.MessageAttachments.Add(new MessageAttachment
                    {
                        AttachmentId = Guid.NewGuid(),
                        MessageId = message.MessageId,
                        MediaUrl = att.MediaUrl,
                        ThumbnailUrl = att.ThumbnailUrl,
                        MediaType = att.MediaType ?? "FILE",
                        FileSize = att.FileSize,
                        DurationSeconds = att.DurationSeconds,
                        Width = att.Width,
                        Height = att.Height,
                        CreatedAt = now
                    });
                }
            }

            _context.Messages.Add(message);

            // Update conversation tracking fields
            conv.LastMessageContent = !string.IsNullOrWhiteSpace(trimmedContent)
                ? trimmedContent
                : (message.MessageAttachments.Count > 0 ? "[Attachment]" : "Sent a message");
            conv.LastMessageSenderId = currentUserId;
            conv.LastMessageAt = now;
            conv.UpdatedAt = now;

            if (isUser1)
            {
                conv.User1LastReadMessageId = message.MessageId;
            }
            else
            {
                conv.User2LastReadMessageId = message.MessageId;
            }

            await _context.SaveChangesAsync();

            var sender = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == currentUserId);

            var broadcastDto = new MessageDto
            {
                Id = message.MessageId,
                ConversationId = message.ConversationId,
                SenderId = message.SenderId,
                SenderName = sender?.FullName ?? sender?.Email ?? "FitSocial User",
                SenderAvatar = sender?.AvatarUrl,
                Content = message.Content,
                MessageType = message.MessageType,
                CreatedAt = message.CreatedAt,
                IsMine = false,
                Attachments = message.MessageAttachments.Select(a => new MessageAttachmentDto
                {
                    AttachmentId = a.AttachmentId,
                    MediaUrl = a.MediaUrl,
                    ThumbnailUrl = a.ThumbnailUrl,
                    MediaType = a.MediaType,
                    FileSize = a.FileSize,
                    DurationSeconds = a.DurationSeconds,
                    Width = a.Width,
                    Height = a.Height
                }).ToList()
            };

            // Broadcast message via SignalR
            var participants = new List<Guid> { currentUserId, otherUserId };
            await _realtimeNotifier.BroadcastMessageAsync(participants, broadcastDto);

            // Send notification to other user
            if (sender != null)
            {
                try
                {
                    await _notificationService.CreateAndSendNewMessageNotificationAsync(message, sender, new List<Guid> { otherUserId });
                }
                catch
                {
                    // Silently ignore notification delivery failure
                }
            }

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
                IsMine = true,
                Attachments = broadcastDto.Attachments
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
            var conv = await _context.Conversations
                .FirstOrDefaultAsync(c => c.ConversationId == conversationId);

            if (conv == null)
            {
                return ApiResponseDto<bool>.Fail("Conversation not found.");
            }

            if (conv.User1Id != currentUserId && conv.User2Id != currentUserId)
            {
                return ApiResponseDto<bool>.Fail("Forbidden: You are not a participant in this conversation.");
            }

            var now = DateTime.UtcNow;
            if (conv.User1Id == currentUserId)
            {
                conv.User1DeletedHistoryAt = now;
            }
            else
            {
                conv.User2DeletedHistoryAt = now;
            }

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
                .FirstOrDefaultAsync(c => c.ConversationId == conversationId);

            if (conv == null)
            {
                return ApiResponseDto<bool>.Fail("Conversation not found.");
            }

            if (conv.User1Id != currentUserId && conv.User2Id != currentUserId)
            {
                return ApiResponseDto<bool>.Fail("Forbidden: You are not a participant in this conversation.");
            }

            var targetUserId = (conv.User1Id == currentUserId ? conv.User2Id : conv.User1Id) ?? Guid.Empty;
            if (targetUserId == Guid.Empty)
            {
                return ApiResponseDto<bool>.Fail("Other participant not found in conversation.");
            }

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
                .FirstOrDefaultAsync(c => c.ConversationId == conversationId);

            if (conv == null)
            {
                return ApiResponseDto<bool>.Fail("Conversation not found.");
            }

            if (conv.User1Id != currentUserId && conv.User2Id != currentUserId)
            {
                return ApiResponseDto<bool>.Fail("Forbidden: You are not a participant in this conversation.");
            }

            var targetUserId = (conv.User1Id == currentUserId ? conv.User2Id : conv.User1Id) ?? Guid.Empty;
            if (targetUserId == Guid.Empty)
            {
                return ApiResponseDto<bool>.Fail("Other participant not found in conversation.");
            }

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

    public async Task<ApiResponseDto<ConversationDetailDto>> GetOrCreateDirectConversationAsync(Guid currentUserId, Guid targetUserId)
    {
        try
        {
            if (currentUserId == targetUserId)
            {
                return ApiResponseDto<ConversationDetailDto>.Fail("Cannot create a conversation with yourself.");
            }

            var targetUserExists = await _context.Users.AnyAsync(u => u.UserId == targetUserId);
            if (!targetUserExists)
            {
                return ApiResponseDto<ConversationDetailDto>.Fail("Target user not found.");
            }

            // Check if 1-1 conversation already exists
            var existingConv = await _context.Conversations
                .FirstOrDefaultAsync(c => (c.User1Id == currentUserId && c.User2Id == targetUserId)
                                       || (c.User1Id == targetUserId && c.User2Id == currentUserId));

            if (existingConv != null)
            {
                return await GetConversationDetailAsync(existingConv.ConversationId, currentUserId);
            }

            // Create new 1-1 conversation
            var now = DateTime.UtcNow;
            var newConv = new Conversation
            {
                ConversationId = Guid.NewGuid(),
                User1Id = currentUserId,
                User2Id = targetUserId,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.Conversations.Add(newConv);
            await _context.SaveChangesAsync();

            return await GetConversationDetailAsync(newConv.ConversationId, currentUserId);
        }
        catch (Exception ex)
        {
            return ApiResponseDto<ConversationDetailDto>.Fail($"System error initializing direct conversation: {ex.Message}");
        }
    }
}
