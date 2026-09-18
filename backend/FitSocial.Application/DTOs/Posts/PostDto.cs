namespace FitSocial.Application.DTOs.Posts;

public class PostDto
{
    public Guid Id { get; set; }
    public Guid AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public string? AuthorAvatarUrl { get; set; }
    public string? Content { get; set; }
    public string PostType { get; set; } = string.Empty;
    public Guid SportId { get; set; }
    public string? SportName { get; set; }
    public Guid LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? LocationAddress { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public bool IsLikedByCurrentUser { get; set; }
    public List<PostMediaDto> Media { get; set; } = new();
}
