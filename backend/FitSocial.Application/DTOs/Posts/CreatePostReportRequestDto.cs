using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Posts;

public class CreatePostReportRequestDto
{
    [Required(ErrorMessage = "Reason is required.")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Reason must be between 1 and 500 characters.")]
    public string Reason { get; set; } = string.Empty;
}
