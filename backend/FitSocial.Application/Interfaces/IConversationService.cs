using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Conversations;

namespace FitSocial.Application.Interfaces;

public interface IConversationService
{
    Task<ApiResponseDto<List<ConversationDto>>> GetUserConversationsAsync(Guid userId);
    Task<ApiResponseDto<ConversationDetailDto>> GetConversationDetailAsync(Guid conversationId, Guid currentUserId);
    Task<ApiResponseDto<MessageDto>> SendMessageAsync(Guid conversationId, Guid currentUserId, SendMessageRequestDto request);
}
