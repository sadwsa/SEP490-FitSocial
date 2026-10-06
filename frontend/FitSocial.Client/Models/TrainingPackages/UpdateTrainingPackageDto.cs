using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FitSocial.Client.Models.TrainingPackages;

public class UpdateTrainingPackageDto
{
    [Required(ErrorMessage = "Package title is required")]
    [MaxLength(255, ErrorMessage = "Package title must not exceed 255 characters")]
    public string? Title { get; set; }

    [MaxLength(500, ErrorMessage = "Description must not exceed 500 characters")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Price is required")]
    [Range(1000, 100000000, ErrorMessage = "Price must be between 1,000 and 100,000,000 VND")]
    public decimal? Price { get; set; }

    [Required(ErrorMessage = "Duration is required")]
    [Range(1, 365, ErrorMessage = "Duration must be between 1 and 365 days")]
    public int? DurationDays { get; set; }

    [Range(1, 365, ErrorMessage = "Session count must be between 1 and 365")]
    public short? SessionCount { get; set; }

    [Range(1, 100, ErrorMessage = "Minimum age must be between 1 and 100")]
    public short? MinAge { get; set; }

    [MaxLength(100, ErrorMessage = "Target audience must not exceed 100 characters")]
    public string? TargetAudience { get; set; }

    public bool? IsActive { get; set; }

    public List<string>? ImageUrls { get; set; }
}
