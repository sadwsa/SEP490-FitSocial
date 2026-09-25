using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Posts;

public class CreatePostReportRequestDto
{
    [Required(ErrorMessage = "Report reason is required.")]
    [StringLength(500, ErrorMessage = "Report reason cannot exceed 500 characters.")]
    public string Reason { get; set; } = string.Empty;
}
