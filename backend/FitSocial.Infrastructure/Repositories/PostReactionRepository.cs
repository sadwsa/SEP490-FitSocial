using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class PostReactionRepository : Repository<PostInteraction>, IPostReactionRepository
{
    public PostReactionRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<PostInteraction?> GetByUserAndPostAsync(Guid postId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(pi => pi.PostId == postId && pi.UserId == userId, cancellationToken);
    }

    public async Task<int> GetReactionCountAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        return await DbSet.CountAsync(pi => pi.PostId == postId, cancellationToken);
    }
}
