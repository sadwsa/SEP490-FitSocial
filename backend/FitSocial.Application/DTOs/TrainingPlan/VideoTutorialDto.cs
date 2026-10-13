using System;

namespace FitSocial.Application.DTOs.TrainingPlan;

public class VideoTutorialDto
{
    public Guid VideoTutorialId { get; set; }
    public Guid CoachId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? VideoUrl { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class CreateVideoTutorialDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string VideoUrl { get; set; } = string.Empty;
}
