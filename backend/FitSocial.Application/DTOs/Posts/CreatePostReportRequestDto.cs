using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Posts;

public class CreatePostReportRequestDto
{
    [Required(ErrorMessage = "Reason is required.")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Reason must be between 1 and 500 characters.")]
    public string Reason { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    [StringLength(30, ErrorMessage = "Type cannot exceed 30 characters.")]
    public string? Type { get; set; } = "POST";

    public List<string>? MediaUrls { get; set; }
}
