using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Posts;

public class CommentDto
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorAvatarUrl { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid? ParentCommentId { get; set; }
    public string? ReplyToAuthorName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<CommentDto> Replies { get; set; } = new();
}

public class CreateCommentRequestDto
{
    [Required(ErrorMessage = "Comment content cannot be empty.")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Comment content must be between 1 and 500 characters.")]
    public string Content { get; set; } = string.Empty;

    public Guid? ParentCommentId { get; set; }
}

public class CreateReplyRequestDto
{
    [Required(ErrorMessage = "Reply content cannot be empty.")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Reply content must be between 1 and 500 characters.")]
    public string Content { get; set; } = string.Empty;
}
