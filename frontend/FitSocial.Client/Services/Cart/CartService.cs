using System.Threading.Tasks;
using FitSocial.Client.Models.Cart;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.Cart;

public class CartService : ICartService
{
    private readonly ApiClient _apiClient;
    private const string BaseEndpoint = "cart";

    public CartService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResponse<CartItemDto>> AddToCartAsync(AddToCartRequestDto request)
    {
        return await _apiClient.PostAsync<AddToCartRequestDto, CartItemDto>($"{BaseEndpoint}/items", request);
    }

    public async Task<ApiResponse<int>> GetCartCountAsync()
    {
        return await _apiClient.GetAsync<int>($"{BaseEndpoint}/count");
    }
}
