using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Posts;

public class CreatePostMediaDto
{
    [Required(ErrorMessage = "MediaUrl is required.")]
    [MaxLength(1000, ErrorMessage = "MediaUrl must not exceed 1000 characters.")]
    public string MediaUrl { get; set; } = string.Empty;

    [Required(ErrorMessage = "MediaType is required.")]
    [RegularExpression("(?i)^(IMAGE|VIDEO)$", ErrorMessage = "MediaType must be either IMAGE or VIDEO.")]
    public string MediaType { get; set; } = string.Empty;
}
