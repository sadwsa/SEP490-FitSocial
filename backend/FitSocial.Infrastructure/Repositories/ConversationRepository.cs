using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class ConversationRepository : Repository<Conversation>, IConversationRepository
{
    public ConversationRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<List<Conversation>> GetUserConversationsWithDetailsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(c => c.User1Id == userId || c.User2Id == userId)
            .Include(c => c.User1)
            .Include(c => c.User2)
            .Include(c => c.Messages)
            .ToListAsync(cancellationToken);
    }

    public async Task<Conversation?> GetConversationWithDetailsAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.User1)
            .Include(c => c.User2)
            .Include(c => c.Messages)
                .ThenInclude(m => m.Sender)
            .Include(c => c.Messages)
                .ThenInclude(m => m.MessageAttachments)
            .FirstOrDefaultAsync(c => c.ConversationId == conversationId, cancellationToken);
    }

    public async Task<Conversation?> GetDirectConversationAsync(Guid user1Id, Guid user2Id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(c => (c.User1Id == user1Id && c.User2Id == user2Id)
                                   || (c.User1Id == user2Id && c.User2Id == user1Id),
                                 cancellationToken);
    }
}
