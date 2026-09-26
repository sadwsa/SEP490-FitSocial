using FitSocial.Application.DTOs.Cart;
using FitSocial.Application.DTOs.Common;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Application.Interfaces
{
    public interface ICartService
    {
        Task<ApiResponseDto<CartSummaryDto>> GetCartAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<ApiResponseDto<int>> GetCartCountAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<ApiResponseDto<CartItemDto>> AddToCartAsync(Guid userId, AddToCartDto dto, CancellationToken cancellationToken = default);
        Task<ApiResponseDto<bool>> RemoveCartItemAsync(Guid userId, Guid cartId, CancellationToken cancellationToken = default);
        Task<ApiResponseDto<bool>> ClearCartAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
