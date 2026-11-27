using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface IPostRepository : IRepository<Post>
{
    Task<Post?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Post?> GetByIdWithMediaAsync(Guid id, CancellationToken cancellationToken = default);
    void RemoveMediaRange(IEnumerable<PostMedium> mediaItems);
    void SetUserRecentCreatedPost(Guid userId, Guid postId);

    Task<(List<Post> Items, int TotalCount)> GetPagedPostsAsync(
        string? postType,
        Guid? locationId,
        Guid? authorId,
        string? searchTerm,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<(List<Post> Items, int TotalCount)> GetPagedPostsAsync(
        string? postType,
        Guid? locationId,
        Guid? authorId,
        string? searchTerm,
        int pageNumber,
        int pageSize,
        string? sortBy,
        DateTime? asOf,
        CancellationToken cancellationToken = default);

    Task<(List<Post> Items, int TotalCount)> GetPagedPostsAsync(
        string? postType,
        Guid? locationId,
        Guid? authorId,
        string? searchTerm,
        int pageNumber,
        int pageSize,
        string? sortBy,
        DateTime? asOf,
        string? sessionId,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);

    Task<(List<Post> Items, int TotalCount)> GetPagedPostsAsync(
        string? postType,
        Guid? locationId,
        Guid? authorId,
        string? searchTerm,
        int pageNumber,
        int pageSize,
        string? sortBy,
        DateTime? asOf,
        string? sessionId,
        Guid? currentUserId,
        Guid? prioritizedPostId,
        CancellationToken cancellationToken = default);
}
