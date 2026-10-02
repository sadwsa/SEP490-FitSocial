using System;
using System.ComponentModel.DataAnnotations;

namespace FitSocial.Client.Models.Cart;

public class AddToCartRequestDto
{
    public Guid? ProductId { get; set; }
    public Guid? PackageId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
    public int Quantity { get; set; } = 1;
}
