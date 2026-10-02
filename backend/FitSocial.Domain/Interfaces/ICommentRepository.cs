using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface ICommentRepository : IRepository<Comment>
{
    Task<List<Comment>> GetCommentsByPostIdAsync(Guid postId, CancellationToken cancellationToken = default);
    Task<Comment?> GetByIdWithAuthorAsync(Guid commentId, CancellationToken cancellationToken = default);
    Task<int> GetCommentCountByPostIdAsync(Guid postId, CancellationToken cancellationToken = default);
}
