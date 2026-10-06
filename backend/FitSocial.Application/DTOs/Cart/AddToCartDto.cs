using System;
using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Cart
{
    public class AddToCartDto
    {
        public Guid? ProductId { get; set; }

        public Guid PackageId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; } = 1;

        public Guid GetEffectivePackageId()
        {
            if (PackageId != Guid.Empty) return PackageId;
            return ProductId ?? Guid.Empty;
        }
    }
}
