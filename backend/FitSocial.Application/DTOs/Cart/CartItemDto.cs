using System;

namespace FitSocial.Application.DTOs.Cart;

public class CartItemDto
{
    public Guid CartId { get; set; }
    public Guid UserId { get; set; }
    public Guid PackageId { get; set; }
    public string? PackageTitle { get; set; }
    public decimal? Price { get; set; }
    public string? CoachName { get; set; }
    public int? DurationDays { get; set; }
    public int Quantity { get; set; }
    public DateTime? CreatedAt { get; set; }
}
