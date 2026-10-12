using System;
using System.Collections.Generic;

namespace FitSocial.Application.DTOs.TrainingPlan;

public class TrainingPlanDetailDto
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

    public List<TrainingPlanExerciseDetailDto> Exercises { get; set; } = new();
}

public class TrainingPlanExerciseDetailDto
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

    public VideoTutorialDto? VideoTutorial { get; set; }
}
