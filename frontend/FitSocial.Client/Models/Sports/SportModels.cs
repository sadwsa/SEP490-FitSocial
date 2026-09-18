using System;
using System.ComponentModel.DataAnnotations;

namespace FitSocial.Client.Models.Sports;

public class SportDto
{
    public Guid SportId { get; set; }
    public string SportName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Category { get; set; } = "Gym & Strength";
    public string Description { get; set; } = string.Empty;
    public string Metrics { get; set; } = string.Empty;
    public string Icon { get; set; } = "dumbbell";
    public int CoachCount { get; set; }
    public int UserCount { get; set; }
    public int PostCount { get; set; }
    public bool Visible { get; set; } = true;
}

public class CreateSportDto
{
    [Required(ErrorMessage = "Sport name is required.")]
    [StringLength(100, ErrorMessage = "Sport name must not exceed 100 characters.")]
    public string SportName { get; set; } = string.Empty;
}

public class UpdateSportDto
{
    [Required(ErrorMessage = "Sport name is required.")]
    [StringLength(100, ErrorMessage = "Sport name must not exceed 100 characters.")]
    public string SportName { get; set; } = string.Empty;
}
