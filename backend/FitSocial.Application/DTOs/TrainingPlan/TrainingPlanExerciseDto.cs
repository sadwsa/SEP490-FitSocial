using System;
using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.TrainingPlan;

public class TrainingPlanExerciseDto
{
    public Guid? TrainingPlanExerciseId { get; set; }

    [Required(ErrorMessage = "Tên bài tập không được để trống.")]
    [MaxLength(200, ErrorMessage = "Tên bài tập tối đa 200 ký tự.")]
    public string ExerciseName { get; set; } = string.Empty;

    [Range(1, 52, ErrorMessage = "WeekNumber phải từ 1 đến 52.")]
    public short WeekNumber { get; set; } = 1;

    [Range(1, 7, ErrorMessage = "DayNumber phải từ 1 (Thứ 2) đến 7 (Chủ nhật).")]
    public short DayNumber { get; set; } = 1;

    public short? Sets { get; set; }

    public short? Reps { get; set; }

    public short? DurationMinutes { get; set; }

    public short? RestSeconds { get; set; }

    [MaxLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự.")]
    public string? Notes { get; set; }

    public short? SortOrder { get; set; }

    public Guid? VideoTutorialId { get; set; }

    public CreateVideoTutorialDto? NewVideoTutorial { get; set; }
}
