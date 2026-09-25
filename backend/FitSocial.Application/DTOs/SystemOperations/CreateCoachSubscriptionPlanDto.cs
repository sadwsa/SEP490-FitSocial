using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.SystemOperations;

public class CreateCoachSubscriptionPlanDto
{
    [Required(ErrorMessage = "Amount is required.")]
    [Range(0, 100000000, ErrorMessage = "Amount must be between 0 and 100,000,000.")]
    public decimal Amount { get; set; }

    [StringLength(10, ErrorMessage = "Currency must not exceed 10 characters.")]
    public string Currency { get; set; } = "VND";

    public bool IsActive { get; set; } = true;

    [StringLength(2048, ErrorMessage = "Image URL must not exceed 2048 characters.")]
    [Url(ErrorMessage = "Image URL must be a valid URL.")]
    public string? ImageUrl { get; set; }

    [StringLength(500, ErrorMessage = "Description must not exceed 500 characters.")]
    public string? Description { get; set; }
}
