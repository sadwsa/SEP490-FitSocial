using System;

namespace FitSocial.Client.Models.SubscriptionPriceHistory;

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

public class GetPriceHistoryFilterRequest
{
    public Guid? PlanId { get; set; }
    public string? Keyword { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
