using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class TrainingPlan
{
    public Guid TrainingPlanId { get; set; }

    public Guid CoachId { get; set; }

    public Guid? OrderDetailsId { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? PublishedAt { get; set; }

    public virtual CoachProfile Coach { get; set; } = null!;

    public virtual OrderDetail? OrderDetails { get; set; }

    public virtual ICollection<TrainingPlanExercise> TrainingPlanExercises { get; set; } = new List<TrainingPlanExercise>();
}
