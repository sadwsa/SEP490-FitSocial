using System.Threading.Tasks;
using FitSocial.Client.Models.Cart;
using FitSocial.Client.Models.Common;

namespace FitSocial.Client.Services.Cart;

public interface ICartService
{
    Task<ApiResponse<CartItemDto>> AddToCartAsync(AddToCartRequestDto request);
    Task<ApiResponse<int>> GetCartCountAsync();
}
