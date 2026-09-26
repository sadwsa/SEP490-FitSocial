using System;
using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Cart
{
    public class AddToCartDto
    {
        [Required]
        public Guid PackageId { get; set; }

        public int Quantity { get; set; } = 1;
    }
}
