namespace FitSocial.Client.Models.Posts;

public class PostDto
{
    public Guid Id { get; set; }
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorAvatar { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<string> MediaUrls { get; set; } = new();
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public bool IsLikedByCurrentUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CreatePostRequest
{
    public string Content { get; set; } = string.Empty;
    public List<string> MediaUrls { get; set; } = new();
}

public class CommentDto
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorAvatar { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
