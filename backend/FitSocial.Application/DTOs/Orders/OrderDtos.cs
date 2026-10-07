using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Orders;

public class CreatePackageOrderRequestDto
{
    [Required]
    public Guid PackageId { get; set; }
}

public class OrderDto
{
    public Guid OrderId { get; set; }
    public Guid BuyerId { get; set; }
    public string? BuyerName { get; set; }
    public string? BuyerEmail { get; set; }
    public Guid? CoachId { get; set; }
    public string? CoachName { get; set; }
    public decimal? TotalAmount { get; set; }
    public string? OrderStatus { get; set; }
    public string? OrderType { get; set; }
    public DateTime? CreatedAt { get; set; }
    public List<OrderDetailDto> Details { get; set; } = new();
}

public class OrderDetailDto
{
    public Guid OrderDetailsId { get; set; }
    public Guid OrderId { get; set; }
    public Guid? PackageId { get; set; }
    public Guid? CoachSubscriptionPlansId { get; set; }
    public string? PackageTitle { get; set; }
    public int? PackageDurationDays { get; set; }
    public string? CoachName { get; set; }
    public decimal? PackagePrice { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class CoachTraineeGroupDto
{
    public Guid TraineeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public int TotalOrdersCount { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime? LatestPurchasedAt { get; set; }
    public List<CoachTraineePackageOrderDto> PurchasedPackages { get; set; } = new();
}

public class CoachTraineePackageOrderDto
{
    public Guid OrderId { get; set; }
    public Guid OrderDetailsId { get; set; }
    public Guid? PackageId { get; set; }
    public string PackageTitle { get; set; } = string.Empty;
    public string? PackageDescription { get; set; }
    public int? DurationDays { get; set; }
    public int? SessionCount { get; set; }
    public decimal? PricePaid { get; set; }
    public string? OrderStatus { get; set; }
    public string? OrderCode { get; set; }
    public DateTime? PurchasedAt { get; set; }
    public string? ThumbnailUrl { get; set; }
    public Guid? TrainingPlanId { get; set; }
    public string? PlanStatus { get; set; }
}
