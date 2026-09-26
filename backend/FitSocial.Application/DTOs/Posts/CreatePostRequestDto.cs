using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Posts;

public class CreatePostRequestDto
{
    [Required(ErrorMessage = "PostType is required.")]
    [MaxLength(50, ErrorMessage = "PostType must not exceed 50 characters.")]
    public string PostType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Location is required.")]
    public Guid LocationId { get; set; }

    [MaxLength(5000, ErrorMessage = "Content must not exceed 5000 characters.")]
    public string? Content { get; set; }

    /// <summary>
    /// List of media items (up to 10 photos or videos).
    /// </summary>
    [MaxLength(10, ErrorMessage = "A post can have at most 10 media items (photos or videos).")]
    public List<CreatePostMediaDto>? Media { get; set; } = new();
}
