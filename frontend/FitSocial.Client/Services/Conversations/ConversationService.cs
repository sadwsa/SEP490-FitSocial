using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Conversations;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.Conversations;

public class ConversationService : IConversationService
{
    private readonly ApiClient _apiClient;

    public ConversationService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResponse<List<ConversationDto>>> GetConversationsAsync()
    {
        return await _apiClient.GetAsync<List<ConversationDto>>("conversations");
    }

    public async Task<ApiResponse<ConversationDetailDto>> GetConversationDetailAsync(Guid conversationId)
    {
        return await _apiClient.GetAsync<ConversationDetailDto>($"conversations/{conversationId}");
    }

    public async Task<ApiResponse<MessageDto>> SendMessageAsync(Guid conversationId, SendMessageRequestDto request)
    {
        return await _apiClient.PostAsync<SendMessageRequestDto, MessageDto>($"conversations/{conversationId}/messages", request);
    }
}
