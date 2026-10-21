using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Conversations;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Application.Services;

public class ConversationService : IConversationService
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IUserBlockRepository _userBlockRepository;
    private readonly IUserRepository _userRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IChatRealtimeNotifier _realtimeNotifier;
    private readonly INotificationService _notificationService;

    public ConversationService(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IUserBlockRepository userBlockRepository,
        IUserRepository userRepository,
        INotificationRepository notificationRepository,
        IUnitOfWork unitOfWork,
        IChatRealtimeNotifier realtimeNotifier,
        INotificationService notificationService)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _userBlockRepository = userBlockRepository;
        _userRepository = userRepository;
        _notificationRepository = notificationRepository;
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
        _notificationService = notificationService;
    }

    public async Task<ApiResponseDto<List<ConversationDto>>> GetUserConversationsAsync(Guid userId)
    {
        try
        {
            // 1. Fetch all conversations where user is User1 or User2 with eager loaded relationships
            var conversations = await _conversationRepository.GetUserConversationsWithDetailsAsync(userId);

            if (!conversations.Any())
            {
                return ApiResponseDto<List<ConversationDto>>.Ok(new List<ConversationDto>(), "Conversations retrieved successfully.");
            }

            var blockedUserIds = await _userBlockRepository.GetBlockedUserIdsAsync(userId);

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
                    .Where(m => IsValidMessageForUser(m, userId, isOtherBlocked))
                    .OrderByDescending(m => m.CreatedAt)
                    .ToList();

                // If user deleted history and there are no messages after deletion, hide
                if (deletedHistoryAt != null && !validMessages.Any())
                {
                    continue;
                }

                var otherUserName = otherUser.FullName ?? otherUser.Email ?? "FitConnect User";

                string? displayLastMessage;
                DateTime? displayLastMessageAt;
                Guid? displayLastMessageSenderId;

                // Always prioritize the latest valid message for display to avoid leaking blocked/auto-reply text
                var latestValidMsg = validMessages.FirstOrDefault();
                if (latestValidMsg != null)
                {
                    displayLastMessage = latestValidMsg.Content;
                    displayLastMessageAt = latestValidMsg.CreatedAt;
                    displayLastMessageSenderId = latestValidMsg.SenderId;
                }
                else
                {
                    displayLastMessage = null;
                    displayLastMessageAt = conv.UpdatedAt ?? conv.CreatedAt;
                    displayLastMessageSenderId = null;
                }

                var dto = new ConversationDto
                {
                    ConversationId = conv.ConversationId,
                    Type = "DIRECT",
                    Title = otherUserName,
                    DisplayAvatar = otherUser.AvatarUrl,
                    OtherUserId = otherUser.UserId,
                    OtherUserName = otherUserName,
                    OtherUserAvatar = otherUser.AvatarUrl,
                    LastMessage = displayLastMessage,
                    LastMessageAt = displayLastMessageAt,
                    LastMessageSenderId = displayLastMessageSenderId,
                    UpdatedAt = conv.UpdatedAt ?? conv.CreatedAt
                };

                // Calculate unread count (if other user is blocked, do not count unread)
                if (isOtherBlocked)
                {
                    dto.UnreadCount = 0;
                }
                else if (lastReadMessageId.HasValue)
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
            var conv = await _conversationRepository.GetConversationWithDetailsAsync(conversationId);

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

            var otherUserName = otherUser?.FullName ?? otherUser?.Email ?? "FitConnect User";

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
                dto.IsBlockedByMe = await _userBlockRepository.IsBlockedAsync(currentUserId, otherUser.UserId);
                dto.IsBlockedByOther = await _userBlockRepository.IsBlockedAsync(otherUser.UserId, currentUserId);
            }

            // Filter messages based on history deletion date and blocking rules
            var validMessages = conv.Messages
                .Where(m => deletedHistoryAt == null || (m.CreatedAt.HasValue && m.CreatedAt > deletedHistoryAt))
                .Where(m => IsValidMessageForUser(m, currentUserId, dto.IsBlockedByMe))
                .OrderBy(m => m.CreatedAt ?? DateTime.MinValue)
                .ToList();

            dto.Messages = validMessages.Select(m => new MessageDto
            {
                Id = m.MessageId,
                ConversationId = m.ConversationId,
                SenderId = m.SenderId,
                SenderName = m.Sender?.FullName ?? m.Sender?.Email ?? "FitConnect User",
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
                bool needsUpdate = false;
                if (isUser1)
                {
                    if (conv.User1LastReadMessageId != latestMessage.MessageId)
                    {
                        conv.User1LastReadMessageId = latestMessage.MessageId;
                        needsUpdate = true;
                    }
                }
                else
                {
                    if (conv.User2LastReadMessageId != latestMessage.MessageId)
                    {
                        conv.User2LastReadMessageId = latestMessage.MessageId;
                        needsUpdate = true;
                    }
                }

                if (needsUpdate)
                {
                    _conversationRepository.Update(conv);
                    await _unitOfWork.SaveChangesAsync();
                }
            }

            // Mark notifications as read
            try
            {
                var messageIds = validMessages.Select(m => m.MessageId).ToList();
                if (messageIds.Any())
                {
                    var unreadConvNotifications = await _notificationRepository.GetUnreadNotificationsByReferenceIdsAsync(currentUserId, messageIds);

                    if (unreadConvNotifications.Any())
                    {
                        foreach (var notif in unreadConvNotifications)
                        {
                            notif.IsRead = true;
                            _notificationRepository.Update(notif);
                        }
                        await _unitOfWork.SaveChangesAsync();
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

            var conv = await _conversationRepository.GetByIdAsync(conversationId);

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
            var isBlockedByMe = await _userBlockRepository.IsBlockedAsync(currentUserId, otherUserId);
            if (isBlockedByMe)
            {
                return ApiResponseDto<MessageDto>.Fail("You have blocked this user. Please unblock them before sending messages.");
            }

            var isBlockedByOther = await _userBlockRepository.IsBlockedAsync(otherUserId, currentUserId);

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

                if (request.Attachments != null && request.Attachments.Any())
                {
                    foreach (var att in request.Attachments)
                    {
                        blockedMessage.MessageAttachments.Add(new MessageAttachment
                        {
                            AttachmentId = Guid.NewGuid(),
                            MessageId = blockedMessage.MessageId,
                            MediaUrl = att.MediaUrl,
                            ThumbnailUrl = att.ThumbnailUrl,
                            MediaType = att.MediaType ?? "IMAGE",
                            FileSize = att.FileSize,
                            DurationSeconds = att.DurationSeconds,
                            Width = att.Width,
                            Height = att.Height,
                            CreatedAt = now
                        });
                    }
                }

                await _messageRepository.AddAsync(blockedMessage);

                var autoReplyMessage = new Message
                {
                    MessageId = Guid.NewGuid(),
                    ConversationId = conversationId,
                    SenderId = otherUserId,
                    Content = "Sorry, I do not want to receive messages from you at the moment.",
                    MessageType = "AUTO",
                    CreatedAt = now.AddSeconds(1)
                };
                await _messageRepository.AddAsync(autoReplyMessage);

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

                _conversationRepository.Update(conv);
                await _unitOfWork.SaveChangesAsync();

                var senderUser = await _userRepository.GetByIdAsync(currentUserId);
                var recipientUser = await _userRepository.GetByIdAsync(otherUserId);

                var autoReplyDto = new MessageDto
                {
                    Id = autoReplyMessage.MessageId,
                    ConversationId = conversationId,
                    SenderId = otherUserId,
                    SenderName = recipientUser?.FullName ?? recipientUser?.Email ?? "FitConnect User",
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
                    SenderName = senderUser?.FullName ?? senderUser?.Email ?? "FitConnect User",
                    SenderAvatar = senderUser?.AvatarUrl,
                    Content = blockedMessage.Content,
                    MessageType = blockedMessage.MessageType,
                    CreatedAt = blockedMessage.CreatedAt,
                    IsMine = true,
                    Attachments = blockedMessage.MessageAttachments.Select(a => new MessageAttachmentDto
                    {
                        AttachmentId = a.AttachmentId,
                        MediaUrl = a.MediaUrl,
                        ThumbnailUrl = a.ThumbnailUrl,
                        MediaType = a.MediaType,
                        FileSize = a.FileSize,
                        DurationSeconds = a.DurationSeconds,
                        Width = a.Width,
                        Height = a.Height
                    }).ToList(),
                    AutoReply = autoReplyDto
                };

                return ApiResponseDto<MessageDto>.Ok(senderMsgDto, "Message sent.");
            }

            // Normal Message Creation
            var messageType = request.MessageType ?? "TEXT";
            if (request.Attachments != null && request.Attachments.Any())
            {
                if (request.Attachments.Any(a => string.Equals(a.MediaType, "VIDEO", StringComparison.OrdinalIgnoreCase)))
                {
                    messageType = "VIDEO";
                }
                else if (request.Attachments.Any(a => string.Equals(a.MediaType, "IMAGE", StringComparison.OrdinalIgnoreCase)))
                {
                    messageType = "IMAGE";
                }
            }

            var message = new Message
            {
                MessageId = Guid.NewGuid(),
                ConversationId = conversationId,
                SenderId = currentUserId,
                Content = trimmedContent,
                MessageType = messageType,
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
                        MediaType = att.MediaType ?? "IMAGE",
                        FileSize = att.FileSize,
                        DurationSeconds = att.DurationSeconds,
                        Width = att.Width,
                        Height = att.Height,
                        CreatedAt = now
                    });
                }
            }

            await _messageRepository.AddAsync(message);

            // Update conversation tracking fields
            conv.LastMessageContent = !string.IsNullOrWhiteSpace(trimmedContent)
                ? trimmedContent
                : (message.MessageAttachments.Count > 0
                    ? (message.MessageAttachments.Any(a => string.Equals(a.MediaType, "VIDEO", StringComparison.OrdinalIgnoreCase)) ? "[Video]" : "[Photo]")
                    : "Sent a message");
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

            _conversationRepository.Update(conv);
            await _unitOfWork.SaveChangesAsync();

            var sender = await _userRepository.GetByIdAsync(currentUserId);

            var broadcastDto = new MessageDto
            {
                Id = message.MessageId,
                ConversationId = message.ConversationId,
                SenderId = message.SenderId,
                SenderName = sender?.FullName ?? sender?.Email ?? "FitConnect User",
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
            var conv = await _conversationRepository.GetByIdAsync(conversationId);

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

            _conversationRepository.Update(conv);
            await _unitOfWork.SaveChangesAsync();

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
            var conv = await _conversationRepository.GetByIdAsync(conversationId);

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

            var existingBlock = await _userBlockRepository.GetBlockAsync(currentUserId, targetUserId);

            if (existingBlock == null)
            {
                var userBlock = new UserBlock
                {
                    Id = Guid.NewGuid(),
                    BlockerId = currentUserId,
                    BlockedId = targetUserId,
                    CreatedAt = DateTime.UtcNow
                };
                await _userBlockRepository.AddAsync(userBlock);
                await _unitOfWork.SaveChangesAsync();
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
            var conv = await _conversationRepository.GetByIdAsync(conversationId);

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

            var existingBlock = await _userBlockRepository.GetBlockAsync(currentUserId, targetUserId);

            if (existingBlock != null)
            {
                _userBlockRepository.Remove(existingBlock);
                await _unitOfWork.SaveChangesAsync();
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

            var targetUser = await _userRepository.GetByIdAsync(targetUserId);
            if (targetUser == null)
            {
                return ApiResponseDto<ConversationDetailDto>.Fail("Target user not found.");
            }

            // Check if 1-1 conversation already exists
            var existingConv = await _conversationRepository.GetDirectConversationAsync(currentUserId, targetUserId);

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

            await _conversationRepository.AddAsync(newConv);
            await _unitOfWork.SaveChangesAsync();

            return await GetConversationDetailAsync(newConv.ConversationId, currentUserId);
        }
        catch (Exception ex)
        {
            return ApiResponseDto<ConversationDetailDto>.Fail($"System error initializing direct conversation: {ex.Message}");
        }
    }

    private static bool IsValidMessageForUser(Message m, Guid userId, bool isOtherBlocked)
    {
        var type = m.MessageType?.ToUpperInvariant();
        bool isBlockType = type == "BLOCK" || type == "BLOCKED";
        bool isAutoType = type == "AUTO" || type == "AUTO_REPLY";

        if (isBlockType)
        {
            // Người nhận (người chặn) không bao giờ nhận/thấy tin nhắn gửi trong lúc bị chặn (kể cả khi đã gỡ chặn)
            if (m.SenderId != userId)
            {
                return false;
            }
            // Nếu chính người gửi hiện tại đang chặn người kia thì cũng không hiện
            if (isOtherBlocked)
            {
                return false;
            }
        }

        if (isAutoType)
        {
            // Người chặn (được gán làm SenderId của auto reply) không bao giờ thấy tin nhắn auto này
            if (m.SenderId == userId)
            {
                return false;
            }
            // Nếu đang chặn thì không hiện
            if (isOtherBlocked)
            {
                return false;
            }
        }

        if (isOtherBlocked && (isBlockType || isAutoType))
        {
            return false;
        }

        return true;
    }
}
