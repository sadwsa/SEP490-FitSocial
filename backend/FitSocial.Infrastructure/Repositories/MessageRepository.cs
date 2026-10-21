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

public class MessageRepository : Repository<Message>, IMessageRepository
{
    public MessageRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<List<Message>> GetMessagesByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(m => m.ConversationId == conversationId)
            .Include(m => m.Sender)
            .Include(m => m.MessageAttachments)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
