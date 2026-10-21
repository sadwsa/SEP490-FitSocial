using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface IMessageRepository : IRepository<Message>
{
    Task<List<Message>> GetMessagesByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default);
}
