using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class CommentRepository : Repository<Comment>, ICommentRepository
{
    public CommentRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<List<Comment>> GetCommentsByPostIdAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.Author)
            .Include(c => c.ParentComment!)
                .ThenInclude(p => p.Author)
            .Where(c => c.PostId == postId)
            .OrderBy(c => c.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<Comment?> GetByIdWithAuthorAsync(Guid commentId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.Author)
            .Include(c => c.ParentComment!)
                .ThenInclude(p => p.Author)
            .FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken);
    }

    public async Task<int> GetCommentCountByPostIdAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        return await DbSet.CountAsync(c => c.PostId == postId, cancellationToken);
    }
}
