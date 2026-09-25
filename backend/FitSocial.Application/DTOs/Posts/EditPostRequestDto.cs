using System.ComponentModel.DataAnnotations;
using FitSocial.Domain.Constants;

namespace FitSocial.Application.DTOs.Posts;

public class EditPostRequestDto
{
    [Required(ErrorMessage = "PostType is required and cannot be null.")]
    [MaxLength(50, ErrorMessage = "PostType must not exceed 50 characters.")]
    public string PostType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Content is required and cannot be null.")]
    [MaxLength(PostConstants.MaxContentLength, ErrorMessage = "Content must not exceed 5000 characters.")]
    public string Content { get; set; } = string.Empty;

    [Required(ErrorMessage = "Location is required and cannot be empty.")]
    public Guid LocationId { get; set; }

    /// <summary>
    /// List of media IDs to remove from the current post.
    /// </summary>
    public List<Guid> RemoveMediaIds { get; set; } = new();

    /// <summary>
    /// List of new media items to upload and attach to the post.
    /// </summary>
    public List<CreatePostMediaDto> NewMedia { get; set; } = new();
}
