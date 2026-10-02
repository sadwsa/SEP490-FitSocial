using System;

namespace FitSocial.Domain.Entities;

/// <summary>
/// Domain model representing a single subscription plan price change audit record.
/// </summary>
public class SubscriptionPriceHistoryItem
{
    public long HistoryId { get; set; }
    public Guid PlanId { get; set; }
    public string? PlanName { get; set; }
    public decimal OldPrice { get; set; }
    public decimal NewPrice { get; set; }
    public DateTime ChangedAt { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public string? ChangedByName { get; set; }
    public string? Notes { get; set; }
}
