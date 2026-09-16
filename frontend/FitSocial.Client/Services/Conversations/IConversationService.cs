using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Conversations;

namespace FitSocial.Client.Services.Conversations;

public interface IConversationService
{
    Task<ApiResponse<List<ConversationDto>>> GetConversationsAsync();
    Task<ApiResponse<ConversationDetailDto>> GetConversationDetailAsync(Guid conversationId);
}
