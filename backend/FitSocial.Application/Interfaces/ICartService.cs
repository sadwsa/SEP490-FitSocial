using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Cart;

namespace FitSocial.Application.Interfaces;

public interface ICartService
{
    Task<CartItemDto> AddToCartAsync(Guid userId, AddToCartRequestDto dto, CancellationToken cancellationToken = default);
    Task<int> GetCartCountAsync(Guid userId, CancellationToken cancellationToken = default);
}
