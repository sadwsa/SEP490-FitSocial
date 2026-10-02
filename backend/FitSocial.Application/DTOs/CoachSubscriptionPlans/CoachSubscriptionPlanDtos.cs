using System;
using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.CoachSubscriptionPlans;

public class CoachSubscriptionPlanDto
{
    public Guid CoachSubscriptionPlansId { get; set; }
    public Guid? PriceId { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public string? Description { get; set; }
    public int? SubscriptionDuration { get; set; }
    public int? TrainingPackageDuration { get; set; }
    public string? ImageUrl { get; set; }
    public bool? IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class CreateCoachSubscriptionPlanDto
{
    public Guid? PriceId { get; set; }

    [Required]
    [Range(0, 1000000000)]
    public decimal Amount { get; set; }

    [StringLength(10)]
    public string Currency { get; set; } = "VND";

    [StringLength(2000)]
    public string? Description { get; set; }

    [Range(1, 3650)]
    public int SubscriptionDuration { get; set; } = 365;

    [Range(0, 3650)]
    public int? TrainingPackageDuration { get; set; }

    [StringLength(2048)]
    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateCoachSubscriptionPlanDto
{
    [Range(0, 1000000000)]
    public decimal? Amount { get; set; }

    [StringLength(10)]
    public string? Currency { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    [Range(1, 3650)]
    public int? SubscriptionDuration { get; set; }

    [Range(0, 3650)]
    public int? TrainingPackageDuration { get; set; }

    [StringLength(2048)]
    public string? ImageUrl { get; set; }

    public bool? IsActive { get; set; }
}
