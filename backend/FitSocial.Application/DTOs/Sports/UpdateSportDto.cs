using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Sports;

public class UpdateSportDto
{
    [Required(ErrorMessage = "Sport name is required.")]
    [StringLength(100, ErrorMessage = "Sport name must not exceed 100 characters.")]
    public string SportName { get; set; } = string.Empty;
}
