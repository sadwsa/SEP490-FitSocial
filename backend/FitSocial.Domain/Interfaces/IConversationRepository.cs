using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface IConversationRepository : IRepository<Conversation>
{
    Task<List<Conversation>> GetUserConversationsWithDetailsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Conversation?> GetConversationWithDetailsAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<Conversation?> GetDirectConversationAsync(Guid user1Id, Guid user2Id, CancellationToken cancellationToken = default);
}
