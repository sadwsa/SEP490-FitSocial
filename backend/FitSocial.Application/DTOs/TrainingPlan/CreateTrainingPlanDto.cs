using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.TrainingPlan;

public class CreateTrainingPlanDto
{
    [Required(ErrorMessage = "OrderDetailsId là bắt buộc.")]
    public Guid OrderDetailsId { get; set; }

    [Required(ErrorMessage = "Tiêu đề kế hoạch không được để trống.")]
    [MaxLength(500, ErrorMessage = "Tiêu đề tối đa 500 ký tự.")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Mô tả tối đa 500 ký tự.")]
    public string? Description { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? Status { get; set; } = "ACTIVE";

    public List<TrainingPlanExerciseDto> Exercises { get; set; } = new();
}
