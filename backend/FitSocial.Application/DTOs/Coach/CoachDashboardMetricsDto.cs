using System;

namespace FitSocial.Application.DTOs.Coach;

/// <summary>
/// Overview performance metrics for the Coach Dashboard.
/// Designed for the Navigation Sidebar "Dashboard & Revenue / Orders" section.
/// </summary>
public class CoachDashboardMetricsDto
{
    /// <summary>
    /// Số lượng học viên đang có gói tập còn hiệu lực với huấn luyện viên.
    /// </summary>
    public int ActiveTraineesCount { get; set; }

    /// <summary>
    /// Tổng số gói tập đã bán thành công (OrderStatus = PAID, COMPLETED, ACTIVE).
    /// </summary>
    public int TotalPackagesSold { get; set; }

    /// <summary>
    /// Doanh thu tháng hiện tại thu được từ việc bán gói tập.
    /// </summary>
    public decimal MonthlyRevenue { get; set; }

    /// <summary>
    /// Tổng số các yêu cầu đang chờ huấn luyện viên xử lý (Meal/Training Plan hoặc đơn hàng chờ xác nhận).
    /// </summary>
    public int PendingRequestsCount { get; set; }

    /// <summary>
    /// Đơn vị tiền tệ hiển thị (mặc định "VND").
    /// </summary>
    public string Currency { get; set; } = "VND";

    /// <summary>
    /// Mốc thời gian hệ thống tổng hợp dữ liệu (UTC).
    /// </summary>
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}
