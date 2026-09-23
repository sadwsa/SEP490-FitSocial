using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class PostRepository : Repository<Post>, IPostRepository
{
    public PostRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<Post?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(p => p.Author)
            .Include(p => p.Location)
            .Include(p => p.PostMedia)
            .Include(p => p.PostInteractions)
            .Include(p => p.Comments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id && (p.IsDeleted == null || p.IsDeleted == false), cancellationToken);
    }

    public async Task<Post?> GetByIdWithMediaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(p => p.PostMedia)
            .Include(p => p.Author)
            .Include(p => p.Location)
            .Include(p => p.PostInteractions)
            .Include(p => p.Comments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id && (p.IsDeleted == null || p.IsDeleted == false), cancellationToken);
    }

    public void RemoveMediaRange(IEnumerable<PostMedium> mediaItems)
    {
        DbContext.PostMedia.RemoveRange(mediaItems);
    }

    public async Task<(List<Post> Items, int TotalCount)> GetPagedPostsAsync(
        string? postType,
        Guid? locationId,
        Guid? authorId,
        string? searchTerm,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking();

        query = query.Where(p => p.IsDeleted == null || p.IsDeleted == false);

        if (!string.IsNullOrWhiteSpace(postType))
        {
            var normalizedType = postType.Trim().ToUpper();
            query = query.Where(p => p.PostType.ToUpper() == normalizedType);
        }

        if (locationId.HasValue && locationId.Value != Guid.Empty)
        {
            query = query.Where(p => p.LocationId == locationId.Value);
        }

        if (authorId.HasValue && authorId.Value != Guid.Empty)
        {
            query = query.Where(p => p.AuthorId == authorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = $"%{searchTerm.Trim()}%";
            query = query.Where(p => p.Content != null && EF.Functions.ILike(p.Content, term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Include(p => p.Author)
            .Include(p => p.Location)
            .Include(p => p.PostMedia)
            .Include(p => p.PostInteractions)
            .Include(p => p.Comments)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
