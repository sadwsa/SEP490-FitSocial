using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Conversations;

namespace FitSocial.Client.Services.Conversations;

public interface IConversationService
{
    Task<ApiResponse<List<ConversationDto>>> GetConversationsAsync();
    Task<ApiResponse<ConversationDetailDto>> GetConversationDetailAsync(Guid conversationId);
    Task<ApiResponse<MessageDto>> SendMessageAsync(Guid conversationId, SendMessageRequestDto request);
    Task<ApiResponse> DeleteConversationAsync(Guid conversationId);
    Task<ApiResponse<bool>> BlockUserAsync(Guid conversationId);
    Task<ApiResponse<bool>> UnblockUserAsync(Guid conversationId);
}
