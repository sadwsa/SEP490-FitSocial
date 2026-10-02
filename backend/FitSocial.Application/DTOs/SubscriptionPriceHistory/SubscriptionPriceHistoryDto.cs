using System;

namespace FitSocial.Application.DTOs.SubscriptionPriceHistory;

/// <summary>
/// Data Transfer Object for Subscription Price History details.
/// </summary>
public class SubscriptionPriceHistoryDto
{
    public long HistoryId { get; set; }
    public Guid PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public decimal OldPrice { get; set; }
    public decimal NewPrice { get; set; }
    public decimal PriceDifference { get; set; }
    public decimal PercentageChange { get; set; }
    public DateTime ChangedAt { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public string? ChangedByName { get; set; }
    public string? Notes { get; set; }
}
