using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class TrainingPlanExercise
{
    public Guid TrainingPlanExerciseId { get; set; }

    public Guid TrainingPlanId { get; set; }

    public Guid? VideoTutorialId { get; set; }

    public short? WeekNumber { get; set; }

    public short? DayNumber { get; set; }

    public string? ExerciseName { get; set; }

    public short? Sets { get; set; }

    public short? Reps { get; set; }

    public short? DurationMinutes { get; set; }

    public short? RestSeconds { get; set; }

    public string? Notes { get; set; }

    public short? SortOrder { get; set; }

    public virtual TrainingPlan TrainingPlan { get; set; } = null!;

    public virtual VideoTutorial? VideoTutorial { get; set; }

    public virtual ICollection<WorkoutCompletionLog> WorkoutCompletionLogs { get; set; } = new List<WorkoutCompletionLog>();
}
