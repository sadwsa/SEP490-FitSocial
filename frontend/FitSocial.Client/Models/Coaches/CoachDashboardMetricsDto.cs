using System;

namespace FitSocial.Client.Models.Coaches;

/// <summary>
/// Overview performance metrics for the Coach Dashboard (UC_17).
/// </summary>
public class CoachDashboardMetricsDto
{
    public int ActiveTraineesCount { get; set; }
    public int TotalPackagesSold { get; set; }
    public decimal MonthlyRevenue { get; set; }
    public int PendingRequestsCount { get; set; }
    public string Currency { get; set; } = "VND";
    public DateTime LastUpdatedAt { get; set; }
}
