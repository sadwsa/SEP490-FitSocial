using System;

namespace FitSocial.Application.DTOs.Sports;

public class SportDto
{
    public Guid SportId { get; set; }
    public string SportName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Category { get; set; } = "General Sports";
    public string Description { get; set; } = string.Empty;
    public string Metrics { get; set; } = string.Empty;
    public string Icon { get; set; } = "dumbbell";
    public int CoachCount { get; set; }
    public int UserCount { get; set; }
    public int PostCount { get; set; }
    public bool Visible { get; set; } = true;
}
