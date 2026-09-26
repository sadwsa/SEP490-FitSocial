using System.Collections.Generic;

namespace FitSocial.Application.DTOs.Cart
{
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
}
