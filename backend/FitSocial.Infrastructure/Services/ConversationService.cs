using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Conversations;
using FitSocial.Application.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Services;

public class ConversationService : IConversationService
{
    private readonly FitSocialDbContext _context;

    public ConversationService(FitSocialDbContext context)
    {
        _context = context;
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

                // Filter messages based on history deletion
                var validMessages = conv.Messages
                    .Where(m => p.HistoryDeletedAt == null || (m.CreatedAt.HasValue && m.CreatedAt > p.HistoryDeletedAt))
                    .OrderByDescending(m => m.CreatedAt)
                    .ToList();

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
            }

            // 4. Filter messages based on history deletion date
            var validMessages = conv.Messages
                .Where(m => currentParticipant.HistoryDeletedAt == null || (m.CreatedAt.HasValue && m.CreatedAt > currentParticipant.HistoryDeletedAt))
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

            return ApiResponseDto<ConversationDetailDto>.Ok(dto, "Conversation detail retrieved successfully.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<ConversationDetailDto>.Fail($"System error retrieving conversation detail: {ex.Message}");
        }
    }
}
