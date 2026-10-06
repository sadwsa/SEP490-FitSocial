using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Analytics;
using FitSocial.Application.DTOs.Common;
using FitSocial.Domain.Interfaces;
using FitSocial.Domain.Models.Analytics;

namespace FitSocial.Application.Services;

public class RevenueAnalyticsService : IRevenueAnalyticsService
{
    private readonly IRevenueAnalyticsRepository _repository;

    public RevenueAnalyticsService(IRevenueAnalyticsRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponseDto<FullRevenueAnalyticsResponseDto>> GetRevenueAnalyticsAsync(
        RevenueAnalyticsFilterDto filter,
        CancellationToken ct = default)
    {
        var (start, end) = ResolveDateRange(filter);
        var granularity = ResolveGranularity(filter.Granularity, start, end);
        var (prevStart, prevEnd) = CalculatePreviousPeriod(start, end);

        // Fetch datasets sequentially because DbContext is not thread-safe for concurrent operations
        var overviewRaw = await _repository.GetRevenueOverviewAsync(start, end, ct);
        var prevOverviewRaw = await _repository.GetRevenueOverviewAsync(prevStart, prevEnd, ct);
        var timeSeriesRaw = await _repository.GetRevenueTimeSeriesAsync(start, end, granularity, ct);
        var breakdownRaw = await _repository.GetRevenueBreakdownAsync(start, end, ct);
        var topCoachesRaw = await _repository.GetTopCoachesByGrossSalesAsync(start, end, 10, ct);
        var topPackagesRaw = await _repository.GetTopSellingPackagesAsync(start, end, 10, ct);
        var planStatsRaw = await _repository.GetCoachSubscriptionPlansStatsAsync(start, end, ct);

        // Platform Direct Revenue = 100% of Coach Subscription Plans - Coach Subscription Refunds
        var platformDirectRevenue = Math.Max(0m, overviewRaw.CoachSubscriptionsGMV - overviewRaw.CoachSubscriptionRefunds);
        var prevPlatformDirectRevenue = Math.Max(0m, prevOverviewRaw.CoachSubscriptionsGMV - prevOverviewRaw.CoachSubscriptionRefunds);

        // Platform Net Cash Holding = Total GMV - Total Coach Payouts - Total Approved Refunds
        var platformNetCashHolding = overviewRaw.TotalGMV - overviewRaw.TotalCoachPayouts - overviewRaw.TotalApprovedRefunds;

        // Average Order Value
        var aov = overviewRaw.CompletedOrdersCount > 0
            ? Math.Round(overviewRaw.TotalGMV / overviewRaw.CompletedOrdersCount, 0)
            : 0m;

        // Growth rate comparison
        double growthRate = 0;
        if (prevOverviewRaw.TotalGMV > 0)
        {
            growthRate = Math.Round((double)((overviewRaw.TotalGMV - prevOverviewRaw.TotalGMV) / prevOverviewRaw.TotalGMV) * 100, 1);
        }
        else if (overviewRaw.TotalGMV > 0)
        {
            growthRate = 100.0;
        }

        var response = new FullRevenueAnalyticsResponseDto
        {
            Overview = new RevenueOverviewDto
            {
                TotalGMV = overviewRaw.TotalGMV,
                TraineePackagesGMV = overviewRaw.TraineePackagesGMV,
                PlatformDirectRevenue = platformDirectRevenue,
                TotalApprovedRefunds = overviewRaw.TotalApprovedRefunds,
                TotalCoachPayouts = overviewRaw.TotalCoachPayouts,
                PlatformNetCashHolding = platformNetCashHolding,
                AverageOrderValue = aov,
                CompletedOrdersCount = overviewRaw.CompletedOrdersCount,
                TotalOrdersCount = overviewRaw.TotalOrdersCount,
                PreviousPeriodTotalGMV = prevOverviewRaw.TotalGMV,
                PreviousPeriodPlatformRevenue = prevPlatformDirectRevenue,
                MoMGrowthRatePercentage = growthRate,
                FromDate = start,
                ToDate = end
            },
            TimeSeries = timeSeriesRaw.Select(p => new RevenueTimeSeriesPointDto
            {
                PeriodLabel = p.PeriodKey,
                Date = p.Date,
                TotalGMV = p.TotalGMV,
                TraineePackageGMV = p.TraineePackageGMV,
                CoachSubscriptionRevenue = p.CoachSubscriptionRevenue,
                ApprovedRefunds = p.ApprovedRefunds
            }).ToList(),
            Breakdown = new RevenueBreakdownDto
            {
                TraineePackageSales = breakdownRaw.TraineePackageSales,
                CoachSubscriptionSales = breakdownRaw.CoachSubscriptionSales,
                TraineePackagePercentage = breakdownRaw.TraineePackagePercentage,
                CoachSubscriptionPercentage = breakdownRaw.CoachSubscriptionPercentage
            },
            TopCoaches = topCoachesRaw.Select(c => new TopCoachRevenueDto
            {
                CoachId = c.CoachId,
                CoachName = c.CoachName,
                CoachEmail = c.CoachEmail,
                AvatarUrl = c.AvatarUrl,
                GrossSales = c.GrossSales,
                CompletedOrdersCount = c.CompletedOrdersCount,
                TotalPayouts = c.TotalPayouts,
                RefundAmount = c.RefundAmount
            }).ToList(),
            TopPackages = topPackagesRaw.Select(p => new TopTrainingPackageDto
            {
                PackageId = p.PackageId,
                Title = p.Title,
                CoachName = p.CoachName,
                Price = p.Price,
                TotalSold = p.TotalSold,
                TotalRevenue = p.TotalRevenue
            }).ToList(),
            SubscriptionPlanStats = planStatsRaw.Select(s => new CoachSubscriptionPlanStatDto
            {
                PlanId = s.PlanId,
                Description = s.Description,
                Price = s.Price,
                DurationDays = s.DurationDays,
                SubscribersCount = s.SubscribersCount,
                TotalRevenue = s.TotalRevenue
            }).ToList()
        };

        return ApiResponseDto<FullRevenueAnalyticsResponseDto>.Ok(response, "System revenue analytics loaded successfully.");
    }

    public async Task<byte[]> ExportRevenueCsvAsync(RevenueAnalyticsFilterDto filter, CancellationToken ct = default)
    {
        var result = await GetRevenueAnalyticsAsync(filter, ct);
        var data = result.Data;
        if (data == null)
        {
            return Array.Empty<byte>();
        }

        var sb = new StringBuilder();
        var viCulture = CultureInfo.GetCultureInfo("vi-VN");

        // UTF-8 BOM so Microsoft Excel renders Vietnamese characters correctly
        sb.Append('\uFEFF');

        // Header: Overview
        sb.AppendLine("=== BÁO CÁO DOANH THU HỆ THỐNG FITSOCIAL ===");
        sb.AppendLine($"Khoảng thời gian:;{data.Overview.FromDate:yyyy-MM-dd HH:mm} đến {data.Overview.ToDate:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"Ngày xuất báo cáo:;{DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine();

        sb.AppendLine("--- 1. CÁC CHỈ SỐ TÀI CHÍNH TỔNG QUAN ---");
        sb.AppendLine($"Tổng GMV Giao dịch Sàn (VND);{data.Overview.TotalGMV.ToString("N0", viCulture)}");
        sb.AppendLine($"GMV Gói tập Trainee (100% Coach, 0% Hoa hồng) (VND);{data.Overview.TraineePackagesGMV.ToString("N0", viCulture)}");
        sb.AppendLine($"Doanh thu Thuần Sàn FitSocial (100% Gói Coach) (VND);{data.Overview.PlatformDirectRevenue.ToString("N0", viCulture)}");
        sb.AppendLine($"Tổng tiền Hoàn trả đã duyệt (Approved Refunds) (VND);{data.Overview.TotalApprovedRefunds.ToString("N0", viCulture)}");
        sb.AppendLine($"Tổng tiền đã Giải ngân cho Coach (Payouts) (VND);{data.Overview.TotalCoachPayouts.ToString("N0", viCulture)}");
        sb.AppendLine($"Dòng tiền ròng Sàn giữ lại (Net Cash Holding) (VND);{data.Overview.PlatformNetCashHolding.ToString("N0", viCulture)}");
        sb.AppendLine($"Giá trị Đơn hàng Trung bình (AOV) (VND);{data.Overview.AverageOrderValue.ToString("N0", viCulture)}");
        sb.AppendLine($"Số Đơn hàng Thành công;{data.Overview.CompletedOrdersCount}");
        sb.AppendLine($"Tổng Đơn hàng Khởi tạo;{data.Overview.TotalOrdersCount}");
        sb.AppendLine($"Tăng trưởng so với Kỳ trước (%);{data.Overview.MoMGrowthRatePercentage:F1}%");
        sb.AppendLine();

        // Section: Time Series
        sb.AppendLine("--- 2. BIẾN ĐỘNG DOANH THU THEO THỜI GIAN ---");
        sb.AppendLine("Kỳ;Thời gian;Tổng GMV (VND);Gói tập Trainee (VND);Gói Coach Subscriptions (VND);Hoàn tiền (VND)");
        foreach (var p in data.TimeSeries)
        {
            sb.AppendLine($"{p.PeriodLabel};{p.Date:yyyy-MM-dd};{p.TotalGMV.ToString("N0", viCulture)};{p.TraineePackageGMV.ToString("N0", viCulture)};{p.CoachSubscriptionRevenue.ToString("N0", viCulture)};{p.ApprovedRefunds.ToString("N0", viCulture)}");
        }
        sb.AppendLine();

        // Section: Top Coaches
        sb.AppendLine("--- 3. TOP 10 COACH DOANH SỐ CAO NHẤT ---");
        sb.AppendLine("Coach ID;Họ tên;Email;Tổng doanh số bán gói tập (VND);Số đơn hoàn thành;Đã Payout (VND);Hoàn tiền (VND)");
        foreach (var c in data.TopCoaches)
        {
            sb.AppendLine($"{c.CoachId};\"{c.CoachName}\";{c.CoachEmail};{c.GrossSales.ToString("N0", viCulture)};{c.CompletedOrdersCount};{c.TotalPayouts.ToString("N0", viCulture)};{c.RefundAmount.ToString("N0", viCulture)}");
        }
        sb.AppendLine();

        // Section: Top Packages
        sb.AppendLine("--- 4. TOP 10 GÓI TẬP BÁN CHẠY NHẤT ---");
        sb.AppendLine("Gói tập ID;Tên gói;Huấn luyện viên;Đơn giá (VND);Số lượt mua;Tổng doanh số (VND)");
        foreach (var pkg in data.TopPackages)
        {
            sb.AppendLine($"{pkg.PackageId};\"{pkg.Title}\";\"{pkg.CoachName}\";{pkg.Price.ToString("N0", viCulture)};{pkg.TotalSold};{pkg.TotalRevenue.ToString("N0", viCulture)}");
        }
        sb.AppendLine();

        // Section: Coach Subscription Plans
        sb.AppendLine("--- 5. HIỆU QUẢ CÁC GÓI THUÊ BAO COACH (COACH SUBSCRIPTIONS) ---");
        sb.AppendLine("Gói Subscription ID;Mô tả;Đơn giá (VND);Thời hạn (Ngày);Số lượt đăng ký;Tổng doanh thu Sàn (VND)");
        foreach (var plan in data.SubscriptionPlanStats)
        {
            sb.AppendLine($"{plan.PlanId};\"{plan.Description}\";{plan.Price.ToString("N0", viCulture)};{plan.DurationDays};{plan.SubscribersCount};{plan.TotalRevenue.ToString("N0", viCulture)}");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static (DateTime start, DateTime end) ResolveDateRange(RevenueAnalyticsFilterDto filter)
    {
        var now = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(filter.QuickFilter))
        {
            var q = filter.QuickFilter.Trim().ToUpperInvariant();
            switch (q)
            {
                case "TODAY":
                    return (DateTime.SpecifyKind(now.Date, DateTimeKind.Unspecified), DateTime.SpecifyKind(now, DateTimeKind.Unspecified));
                case "7D":
                    return (DateTime.SpecifyKind(now.Date.AddDays(-6), DateTimeKind.Unspecified), DateTime.SpecifyKind(now, DateTimeKind.Unspecified));
                case "THIS_MONTH":
                    return (new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Unspecified), DateTime.SpecifyKind(now, DateTimeKind.Unspecified));
                case "LAST_MONTH":
                    var lm = now.AddMonths(-1);
                    var lmStart = new DateTime(lm.Year, lm.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
                    var lmEnd = new DateTime(lm.Year, lm.Month, DateTime.DaysInMonth(lm.Year, lm.Month), 23, 59, 59, 999, DateTimeKind.Unspecified);
                    return (lmStart, lmEnd);
                case "THIS_YEAR":
                    return (new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), DateTime.SpecifyKind(now, DateTimeKind.Unspecified));
            }
        }

        if (filter.FromDate.HasValue && filter.ToDate.HasValue)
        {
            var s = DateTime.SpecifyKind(filter.FromDate.Value.Date, DateTimeKind.Unspecified);
            var e = DateTime.SpecifyKind(filter.ToDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Unspecified);
            return (s, e);
        }

        if (filter.FromDate.HasValue)
        {
            var s = DateTime.SpecifyKind(filter.FromDate.Value.Date, DateTimeKind.Unspecified);
            return (s, DateTime.SpecifyKind(now, DateTimeKind.Unspecified));
        }

        // Default: Start of current month to now
        return (new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Unspecified), DateTime.SpecifyKind(now, DateTimeKind.Unspecified));
    }

    private static string ResolveGranularity(string? requestedGranularity, DateTime start, DateTime end)
    {
        if (!string.IsNullOrWhiteSpace(requestedGranularity))
        {
            var g = requestedGranularity.Trim().ToUpperInvariant();
            if (g is "DAILY" or "WEEKLY" or "MONTHLY")
            {
                return g;
            }
        }

        var totalDays = (end - start).TotalDays;
        if (totalDays <= 31) return "DAILY";
        if (totalDays <= 90) return "WEEKLY";
        return "MONTHLY";
    }

    private static (DateTime prevStart, DateTime prevEnd) CalculatePreviousPeriod(DateTime start, DateTime end)
    {
        var duration = end - start;
        var prevEnd = start.AddTicks(-1);
        var prevStart = prevEnd.Subtract(duration);
        return (prevStart, prevEnd);
    }
}
