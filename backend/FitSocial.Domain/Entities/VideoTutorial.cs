using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class VideoTutorial
{
    public Guid VideoTutorialId { get; set; }

    public Guid CoachId { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? VideoUrl { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual CoachProfile Coach { get; set; } = null!;

    public virtual ICollection<TrainingPlanExercise> TrainingPlanExercises { get; set; } = new List<TrainingPlanExercise>();
}
