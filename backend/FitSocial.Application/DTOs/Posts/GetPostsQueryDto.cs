namespace FitSocial.Application.DTOs.Posts;

public class GetPostsQueryDto
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? PostType { get; set; }
    public Guid? SportId { get; set; }
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
    /// Returns the effective trimmed keyword if provided, otherwise null.
    /// </summary>
    public string? EffectiveKeyword =>
        !string.IsNullOrWhiteSpace(Keyword) ? Keyword.Trim() :
        (!string.IsNullOrWhiteSpace(SearchTerm) ? SearchTerm.Trim() : null);
}
