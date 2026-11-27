using System;

namespace FitSocial.Domain.Entities;

public partial class MealCompletionLog
{
    public Guid MealCompletionLogId { get; set; }

    public Guid TraineeId { get; set; }

    public Guid MealPlanItemId { get; set; }

    public DateTime? LogDate { get; set; }

    public string? Status { get; set; }

    public string? Notes { get; set; }

    public string? MediaUrl { get; set; }

    public string? CoachFeedback { get; set; }

    public Guid? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User Trainee { get; set; } = null!;

    public virtual User? ReviewedByCoach { get; set; }

    public virtual MealPlanItem MealPlanItem { get; set; } = null!;
}
