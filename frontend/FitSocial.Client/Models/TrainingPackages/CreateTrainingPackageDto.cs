using System.ComponentModel.DataAnnotations;

namespace FitSocial.Client.Models.TrainingPackages;

public class CreateTrainingPackageDto
{
    [Required(ErrorMessage = "Title is required")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Range(0.01, 10000000, ErrorMessage = "Price must be greater than 0")]
    public decimal Price { get; set; }

    [Required]
    [Range(1, 365, ErrorMessage = "Duration must be between 1 and 365 days")]
    public int DurationDays { get; set; }

    public bool IsActive { get; set; } = true;
}
