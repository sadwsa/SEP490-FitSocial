using System;
using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.SubscriptionPriceHistory;

/// <summary>
/// Query filter and pagination parameters for viewing subscription price history.
/// </summary>
public class GetPriceHistoryFilterDto
{
    public Guid? PlanId { get; set; }

    public string? Keyword { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "PageIndex must be greater than or equal to 1.")]
    public int PageIndex { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100.")]
    public int PageSize { get; set; } = 10;
}
