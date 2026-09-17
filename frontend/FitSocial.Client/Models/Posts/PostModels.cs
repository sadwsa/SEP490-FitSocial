namespace FitSocial.Client.Models.Posts;

public class PostDto
{
    public Guid Id { get; set; }
    public Guid AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public string? AuthorAvatarUrl { get; set; }

    // Helper property for backward compatibility
    public string? AuthorAvatar
    {
        get => AuthorAvatarUrl;
        set => AuthorAvatarUrl = value;
    }

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

    // Helper for backward compatibility
    public List<string> MediaUrls
    {
        get => Media?.Select(m => m.MediaUrl).ToList() ?? new List<string>();
        set
        {
            if (value != null)
            {
                Media = value.Select(u => new PostMediaDto { MediaUrl = u, MediaType = "IMAGE" }).ToList();
            }
        }
    }
}

public class PostMediaDto
{
    public Guid Id { get; set; }
    public string MediaUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = "IMAGE";
    public DateTime? CreatedAt { get; set; }
}

public class CreatePostRequest
{
    public string? Content { get; set; }
    public string PostType { get; set; } = "Normal";
    public Guid SportId { get; set; }
    public Guid LocationId { get; set; }
    public List<CreatePostMediaItemDto> Media { get; set; } = new();
    public List<string> MediaUrls { get; set; } = new();
}

public class CreatePostMediaItemDto
{
    public string MediaUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = "IMAGE";
}

public class GetPostsQuery
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? PostType { get; set; }
    public Guid? SportId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? AuthorId { get; set; }
    public string? SearchTerm { get; set; }
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
