namespace FitSocial.Client.Models.SystemOperations;

public class CoachSubscriptionPlanDto
{
    public Guid PriceId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public bool IsActive { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public int SubscriberCount { get; set; }
    public int ActiveSubscriberCount { get; set; }
}

public class CoachUpgradeDto
{
    public Guid UpgradeId { get; set; }
    public Guid PriceId { get; set; }
    public Guid CoachId { get; set; }
    public string CoachName { get; set; } = string.Empty;
    public string CoachEmail { get; set; } = string.Empty;
    public string CoachAvatarUrl { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public decimal PriceAmount { get; set; }
    public string PriceCurrency { get; set; } = "VND";
}

public class CoachSubscriptionPlansResponseDto
{
    public List<CoachSubscriptionPlanDto> Plans { get; set; } = new();
    public int TotalPlans { get; set; }
    public int ActivePlansCount { get; set; }
}
