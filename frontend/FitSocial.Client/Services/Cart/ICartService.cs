using FitSocial.Client.Models.Cart;
using FitSocial.Client.Models.Common;
using System;
using System.Threading.Tasks;

namespace FitSocial.Client.Services.Cart
{
    public interface ICartService
    {
        Task<ApiResponse<CartSummaryDto>> GetCartAsync();
        Task<ApiResponse<int>> GetCartCountAsync();
        Task<ApiResponse<CartItemDto>> AddToCartAsync(AddToCartDto dto);
        Task<ApiResponse<bool>> RemoveCartItemAsync(Guid cartId);
        Task<ApiResponse<bool>> ClearCartAsync();

        event Action? OnCartChanged;
        void NotifyCartChanged();
    }
}
