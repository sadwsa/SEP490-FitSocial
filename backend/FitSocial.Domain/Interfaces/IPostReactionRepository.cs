using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface IPostReactionRepository : IRepository<PostInteraction>
{
    /// <summary>
    /// Retrieves a reaction by post and user ID.
    /// </summary>
    Task<PostInteraction?> GetByUserAndPostAsync(Guid postId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts total active reactions on a post.
    /// </summary>
    Task<int> GetReactionCountAsync(Guid postId, CancellationToken cancellationToken = default);
}
