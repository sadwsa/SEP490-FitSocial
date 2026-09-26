using FitSocial.Application.DTOs.Cart;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = $"{RoleConstants.Trainee},{RoleConstants.Coach}")]
    public class CartsController : ControllerBase
    {
        private readonly ICartService _cartService;

        public CartsController(ICartService cartService)
        {
            _cartService = cartService;
        }

        private bool TryGetCurrentUserId(out Guid userId)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdStr, out userId);
        }

        /// <summary>
        /// Get current user's cart summary and items (View Cart)
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponseDto<CartSummaryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCart(CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized(ApiResponseDto<CartSummaryDto>.Fail("Phiên đăng nhập không hợp lệ."));
            }

            var result = await _cartService.GetCartAsync(userId, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Get number of items in the cart for header badge counter
        /// </summary>
        [HttpGet("count")]
        [ProducesResponseType(typeof(ApiResponseDto<int>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCartCount(CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized(ApiResponseDto<int>.Fail("Phiên đăng nhập không hợp lệ."));
            }

            var result = await _cartService.GetCartCountAsync(userId, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Add a training package to the cart
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponseDto<CartItemDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartDto dto, CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized(ApiResponseDto<CartItemDto>.Fail("Phiên đăng nhập không hợp lệ."));
            }

            var result = await _cartService.AddToCartAsync(userId, dto, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Remove a single item from the cart
        /// </summary>
        [HttpDelete("{cartId:guid}")]
        [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status200OK)]
        public async Task<IActionResult> RemoveCartItem([FromRoute] Guid cartId, CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized(ApiResponseDto<bool>.Fail("Phiên đăng nhập không hợp lệ."));
            }

            var result = await _cartService.RemoveCartItemAsync(userId, cartId, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Clear all items from current user's cart
        /// </summary>
        [HttpDelete("clear")]
        [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ClearCart(CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized(ApiResponseDto<bool>.Fail("Phiên đăng nhập không hợp lệ."));
            }

            var result = await _cartService.ClearCartAsync(userId, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
    }
}
