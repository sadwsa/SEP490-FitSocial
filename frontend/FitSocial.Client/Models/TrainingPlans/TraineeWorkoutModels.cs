using System;
using System.Collections.Generic;

namespace FitSocial.Client.Models.TrainingPlans;

public class ToggleWorkoutCompletionDto
{
    public Guid TrainingPlanExerciseId { get; set; }
    public DateTime? LogDate { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
}

public class WorkoutCompletionResultDto
{
    public Guid TrainingPlanExerciseId { get; set; }
    public bool IsCompleted { get; set; }
    public Guid? WorkoutCompletionLogId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
    public int TotalExercisesCount { get; set; }
    public int CompletedExercisesCount { get; set; }
    public double ProgressPercentage { get; set; }
}

public class TraineeWorkoutExerciseDto
{
    public Guid TrainingPlanExerciseId { get; set; }
    public Guid TrainingPlanId { get; set; }
    public short? WeekNumber { get; set; }
    public short? DayNumber { get; set; }
    public string ExerciseName { get; set; } = string.Empty;
    public short? Sets { get; set; }
    public short? Reps { get; set; }
    public short? DurationMinutes { get; set; }
    public short? RestSeconds { get; set; }
    public string? Notes { get; set; }
    public short? SortOrder { get; set; }
    public VideoTutorialDto? VideoTutorial { get; set; }

    public bool IsCompleted { get; set; }
    public Guid? WorkoutCompletionLogId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? TraineeNotes { get; set; }

    /// <summary>COMPLETED, INCOMPLETE, or null when not logged yet.</summary>
    public string? Status { get; set; }

    /// <summary>Calendar date this exercise is scheduled for.</summary>
    public DateTime? ScheduledDate { get; set; }
}

public class TraineeWorkoutDayDto
{
    public short DayNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public int TotalExercises { get; set; }
    public int CompletedExercises { get; set; }
    public bool IsAllCompleted => TotalExercises > 0 && CompletedExercises >= TotalExercises;
    public List<TraineeWorkoutExerciseDto> Exercises { get; set; } = new();

    /// <summary>Calendar date for this day; null when outside the plan range.</summary>
    public DateTime? ScheduledDate { get; set; }

    /// <summary>Only today can be marked complete.</summary>
    public bool IsToday { get; set; }
}

public class TraineeWorkoutWeekDto
{
    public short WeekNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public int TotalExercises { get; set; }
    public int CompletedExercises { get; set; }
    public double ProgressPercentage => TotalExercises > 0 ? Math.Round((double)CompletedExercises / TotalExercises * 100, 1) : 0;
    public List<TraineeWorkoutDayDto> Days { get; set; } = new();
}

public class TraineeWorkoutPlanProgressDto
{
    public Guid TrainingPlanId { get; set; }
    public Guid OrderDetailsId { get; set; }
    public Guid PackageId { get; set; }
    public string PackageTitle { get; set; } = string.Empty;
    public string? PackageThumbnailUrl { get; set; }
    public string PlanTitle { get; set; } = string.Empty;
    public string? PlanDescription { get; set; }
    public string? PlanStatus { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public Guid CoachId { get; set; }
    public string CoachName { get; set; } = string.Empty;
    public string? CoachAvatarUrl { get; set; }

    public int TotalExercisesCount { get; set; }
    public int CompletedExercisesCount { get; set; }
    public double ProgressPercentage => TotalExercisesCount > 0 ? Math.Round((double)CompletedExercisesCount / TotalExercisesCount * 100, 1) : 0;

    public List<TraineeWorkoutWeekDto> Weeks { get; set; } = new();
}

public class CoachTraineeWorkoutProgressDto
{
    public Guid OrderDetailsId { get; set; }
    public Guid TraineeId { get; set; }
    public string TraineeName { get; set; } = string.Empty;
    public string TraineeEmail { get; set; } = string.Empty;
    public string? TraineeAvatarUrl { get; set; }
    public string? TraineePhone { get; set; }

    public Guid PackageId { get; set; }
    public string PackageTitle { get; set; } = string.Empty;
    public Guid? TrainingPlanId { get; set; }
    public string? PlanTitle { get; set; }
    public string? PlanStatus { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public int TotalExercisesCount { get; set; }
    public int CompletedExercisesCount { get; set; }
    public double ProgressPercentage => TotalExercisesCount > 0 ? Math.Round((double)CompletedExercisesCount / TotalExercisesCount * 100, 1) : 0;

    public List<TraineeWorkoutWeekDto> Weeks { get; set; } = new();
    public List<CoachWorkoutActivityLogDto> RecentLogs { get; set; } = new();
}

public class CoachWorkoutActivityLogDto
{
    public Guid LogId { get; set; }
    public string ExerciseName { get; set; } = string.Empty;
    public short? WeekNumber { get; set; }
    public short? DayNumber { get; set; }
    public DateTime CompletedAt { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
}
