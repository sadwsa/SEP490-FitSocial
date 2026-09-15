using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class WorkoutCompletionLog
{
    public Guid WorkoutCompletionLogId { get; set; }

    public Guid TraineeId { get; set; }

    public Guid TrainingPlanExerciseId { get; set; }

    public DateTime LogDate { get; set; }

    public string? Status { get; set; }

    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User Trainee { get; set; } = null!;

    public virtual TrainingPlanExercise TrainingPlanExercise { get; set; } = null!;
}
