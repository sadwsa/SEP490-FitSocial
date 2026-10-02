using System;
using System.Collections.Generic;

namespace FitSocial.Client.Models.Cart
{
    public class CartItemDto
    {
        public Guid CartId { get; set; }
        public Guid PackageId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int? DurationDays { get; set; }
        public string DurationLabel { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; } = 1;
        public Guid CoachId { get; set; }
        public string CoachName { get; set; } = string.Empty;
        public string? CoachAvatarUrl { get; set; }
        public string? SportName { get; set; }
        public int? ExperienceYears { get; set; }
        public double AverageRating { get; set; } = 5.0;
        public int ReviewCount { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public bool IsAvailable { get; set; } = true;
        public string? UnavailableReason { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class CartSummaryDto
    {
        public List<CartItemDto> Items { get; set; } = new();
        public int TotalItems { get; set; }
        public int ValidItemsCount { get; set; }
        public decimal Subtotal { get; set; }
        public decimal PlatformFee { get; set; } = 0;
        public decimal TotalPrice { get; set; }
        public decimal ApproxUsd { get; set; }
        public bool HasUnavailableItems { get; set; }
        public bool IsCoach { get; set; }
        public string TemporaryOrderCode { get; set; } = string.Empty;
    }

    public class AddToCartDto
    {
        public Guid PackageId { get; set; }
        public int Quantity { get; set; } = 1;
    }
}
