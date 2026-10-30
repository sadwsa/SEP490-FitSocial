namespace FitSocial.Application.DTOs.Payments;

public class CoachSubscriptionStatusDto
{
    public string Status { get; set; } = "NONE"; // ACTIVE / EXPIRED / NONE
    public DateTime? EndDay { get; set; }
    public Guid? PlanId { get; set; }
    public string? PlanDescription { get; set; }
    public decimal? Amount { get; set; }
    public int? SubscriptionDurationMonths { get; set; }
}