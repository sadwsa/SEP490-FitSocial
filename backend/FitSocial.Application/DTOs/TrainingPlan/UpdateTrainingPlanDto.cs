using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.TrainingPlan;

public class UpdateTrainingPlanDto
{
    [MaxLength(500, ErrorMessage = "Tiêu đề tối đa 500 ký tự.")]
    public string? Title { get; set; }

    [MaxLength(500, ErrorMessage = "Mô tả tối đa 500 ký tự.")]
    public string? Description { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? Status { get; set; }

    public List<TrainingPlanExerciseDto>? Exercises { get; set; }
}
