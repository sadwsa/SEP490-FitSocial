namespace FitSocial.Application.DTOs.Posts;

public class GetPostsQueryDto
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? PostType { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? AuthorId { get; set; }

    /// <summary>
    /// Search keyword to match against Post.Content
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    /// Backward-compatible alias for Keyword
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Feed sorting mode: "hot" (default trending), "newest"
    /// </summary>
    public string? SortBy { get; set; } = "hot";

    /// <summary>
    /// Anchor timestamp for the feed session to ensure consistent HoursElapsed calculation across paginated requests.
    /// </summary>
    public DateTime? AsOf { get; set; }

    /// <summary>
    /// Feed session identifier for snapshot-based ranking freeze.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Optional post ID to prioritize at the head of the feed for the post's author.
    /// </summary>
    public Guid? PrioritizedPostId { get; set; }

    /// <summary>
    /// Returns the effective trimmed keyword if provided, otherwise null.
    /// </summary>
    public string? EffectiveKeyword =>
        !string.IsNullOrWhiteSpace(Keyword) ? Keyword.Trim() :
        (!string.IsNullOrWhiteSpace(SearchTerm) ? SearchTerm.Trim() : null);
}
