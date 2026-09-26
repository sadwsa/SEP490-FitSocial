using FitSocial.Client.Models.Cart;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Services.Http;
using System;
using System.Threading.Tasks;

namespace FitSocial.Client.Services.Cart
{
    public class CartService : ICartService
    {
        private readonly ApiClient _apiClient;

        public event Action? OnCartChanged;

        public CartService(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public void NotifyCartChanged()
        {
            OnCartChanged?.Invoke();
        }

        public async Task<ApiResponse<CartSummaryDto>> GetCartAsync()
        {
            return await _apiClient.GetAsync<CartSummaryDto>("carts");
        }

        public async Task<ApiResponse<int>> GetCartCountAsync()
        {
            return await _apiClient.GetAsync<int>("carts/count");
        }

        public async Task<ApiResponse<CartItemDto>> AddToCartAsync(AddToCartDto dto)
        {
            var response = await _apiClient.PostAsync<AddToCartDto, CartItemDto>("carts", dto);
            if (response.Success)
            {
                NotifyCartChanged();
            }
            return response;
        }

        public async Task<ApiResponse<bool>> RemoveCartItemAsync(Guid cartId)
        {
            var response = await _apiClient.DeleteAsync<bool>($"carts/{cartId}");
            if (response.Success)
            {
                NotifyCartChanged();
            }
            return response;
        }

        public async Task<ApiResponse<bool>> ClearCartAsync()
        {
            var response = await _apiClient.DeleteAsync<bool>("carts/clear");
            if (response.Success)
            {
                NotifyCartChanged();
            }
            return response;
        }
    }
}
