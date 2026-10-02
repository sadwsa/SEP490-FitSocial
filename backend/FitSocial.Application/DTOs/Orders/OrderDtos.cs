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
