using FitSocial.Application.DTOs.Auth;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Orders;
using FitSocial.Application.DTOs.Payments;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IOrderRepository _orderRepository;

    public PaymentsController(
        IPaymentService paymentService,
        IPaymentRepository? paymentRepository = null,
        IOrderRepository? orderRepository = null)
    {
        _paymentService = paymentService;
        _paymentRepository = paymentRepository!;
        _orderRepository = orderRepository!;
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdStr, out userId);
    }

    private string? FirstModelError()
    {
        return ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
    }

    /// <summary>
    /// Validate the coach draft (model + email availability) BEFORE paying.
    /// </summary>
    [HttpPost("coach-activation/prepare")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponseDto<CoachActivationPreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<CoachActivationPreviewDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PrepareCoachActivation([FromBody] RegisterCoachRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponseDto<CoachActivationPreviewDto>.Fail(
                FirstModelError() ?? "Invalid data"));
        }

        var result = await _paymentService.PrepareCoachActivationAsync(request);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Verify OTP + lock in the coach draft, then return a PayOS VietQR payment link.
    /// Call this when the user clicks Pay on the activation page.
    /// </summary>
    [HttpPost("coach-activation/create-link")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponseDto<ActivationLinkDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<ActivationLinkDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateActivationLink(
        [FromBody] RegisterCoachRequestDto request,
        [FromQuery] string? originUrl = null)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponseDto<ActivationLinkDto>.Fail(
                FirstModelError() ?? "Invalid data"));
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _paymentService.CreateActivationLinkAsync(request, originUrl ?? string.Empty, ipAddress);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    public record CompleteActivationRequest(long OrderCode);

    /// <summary>
    /// Check the PayOS payment by order code; if paid, unlock the account and sign the coach in.
    /// Called by the frontend payment-result page after the gateway redirects back.
    /// </summary>
    [HttpPost("coach-activation/complete")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponseDto<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<AuthResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompleteActivation([FromBody] CompleteActivationRequest request)
    {
        var result = await _paymentService.CompleteActivationAsync(request.OrderCode);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    public record CancelActivationRequest(long OrderCode);

    /// <summary>
    /// Cancel a pending activation order (user gave up at the gateway).
    /// </summary>
    [HttpPost("coach-activation/cancel")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelActivation([FromBody] CancelActivationRequest request)
    {
        var result = await _paymentService.CancelActivationAsync(request.OrderCode);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// PayOS webhook (backup for the return-url flow; requires a public URL in production).
    /// Routes by OrderType: COACH_ACTIVATION unlocks the account, PACKAGE activates enrollment.
    /// </summary>
    [HttpPost("payos-webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> PayOSWebhook([FromBody] System.Text.Json.JsonElement payload)
    {
        try
        {
            if (!payload.TryGetProperty("data", out var data))
            {
                return Ok(new { success = false });
            }

            var code = data.TryGetProperty("code", out var codeEl) ? codeEl.GetString() : null;
            if (!string.Equals(code, "00", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new { success = true });
            }

            long orderCode = 0;
            if (data.TryGetProperty("orderCode", out var orderEl))
            {
                if (orderEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    orderCode = orderEl.GetInt64();
                }
                else if (orderEl.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    long.TryParse(orderEl.GetString(), out orderCode);
                }
            }

            if (orderCode == 0)
            {
                return Ok(new { success = false });
            }

            // Route webhook to the correct fulfillment flow by order type
            if (_paymentRepository != null && _orderRepository != null)
            {
                var payment = await _paymentRepository.FindByGatewayTransactionIdAsync(orderCode.ToString());
                if (payment != null)
                {
                    var order = await _orderRepository.GetByIdAsync(payment.OrderId);
                    if (order != null && string.Equals(order.OrderType, PaymentConstants.OrderTypePackage, StringComparison.OrdinalIgnoreCase))
                    {
                        await _paymentService.CompletePackagePurchaseAsync(orderCode);
                        return Ok(new { success = true });
                    }
                }
            }

            await _paymentService.CompleteActivationAsync(orderCode);
            return Ok(new { success = true });
        }
        catch
        {
            // Never fail the webhook handshake because of our errors
            return Ok(new { success = false });
        }
    }

    // =====================================================
    // UC-20: Purchase Training Package (Trainee, Coach)
    // =====================================================

    public record PackagePurchaseRequest(Guid PackageId);

    /// <summary>
    /// UC-20 step 2: Order Summary preview (package title, duration, price VND).
    /// E1: package inactive -&gt; 400, E2: self-purchase -&gt; 400.
    /// </summary>
    [HttpPost("package/purchase/prepare")]
    [Authorize(Roles = $"{RoleConstants.Trainee},{RoleConstants.Coach}")]
    [ProducesResponseType(typeof(ApiResponseDto<PackagePurchasePreviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreparePackagePurchase([FromBody] PackagePurchaseRequest request)
    {
        if (!TryGetCurrentUserId(out var buyerId))
        {
            return Unauthorized(ApiResponseDto<PackagePurchasePreviewDto>.Fail("Unauthorized"));
        }

        if (request == null || request.PackageId == Guid.Empty)
        {
            return BadRequest(ApiResponseDto<PackagePurchasePreviewDto>.Fail("Package ID is required."));
        }

        var result = await _paymentService.PreparePackagePurchaseAsync(buyerId, request.PackageId);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// UC-20 step 4: create pending order + PayOS VietQR transaction.
    /// </summary>
    [HttpPost("package/purchase/create-link")]
    [Authorize(Roles = $"{RoleConstants.Trainee},{RoleConstants.Coach}")]
    [ProducesResponseType(typeof(ApiResponseDto<PackagePaymentLinkDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreatePackagePaymentLink(
        [FromBody] PackagePurchaseRequest request,
        [FromQuery] string? originUrl = null)
    {
        if (!TryGetCurrentUserId(out var buyerId))
        {
            return Unauthorized(ApiResponseDto<PackagePaymentLinkDto>.Fail("Unauthorized"));
        }

        if (request == null || request.PackageId == Guid.Empty)
        {
            return BadRequest(ApiResponseDto<PackagePaymentLinkDto>.Fail("Package ID is required."));
        }

        var frontend = string.IsNullOrWhiteSpace(originUrl)
            ? $"{Request.Scheme}://{Request.Host}"
            : originUrl;
        var result = await _paymentService.CreatePackagePaymentLinkAsync(buyerId, request.PackageId, frontend);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    public record CompletePackagePurchaseRequest(long OrderCode);

    /// <summary>
    /// UC-20 steps 6-7: verify PayOS payment; on success mark COMPLETED + activate TrainingPlan.
    /// Called by "I've Paid - Verify Now" and by the success page after gateway redirect.
    /// E3: timeout/failure -&gt; 400 so the user can retry.
    /// </summary>
    [HttpPost("package/purchase/complete")]
    [Authorize(Roles = $"{RoleConstants.Trainee},{RoleConstants.Coach}")]
    [ProducesResponseType(typeof(ApiResponseDto<OrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompletePackagePurchase([FromBody] CompletePackagePurchaseRequest request)
    {
        if (request == null || request.OrderCode <= 0)
        {
            return BadRequest(ApiResponseDto<OrderDto>.Fail("Order code is required."));
        }

        var result = await _paymentService.CompletePackagePurchaseAsync(request.OrderCode);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    public record CancelPackagePurchaseRequest(long OrderCode);

    /// <summary>
    /// UC-20 alternative flow 20.1: cancel pending checkout, keep item in cart.
    /// </summary>
    [HttpPost("package/purchase/cancel")]
    [Authorize(Roles = $"{RoleConstants.Trainee},{RoleConstants.Coach}")]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelPackagePurchase([FromBody] CancelPackagePurchaseRequest request)
    {
        if (!TryGetCurrentUserId(out var buyerId))
        {
            return Unauthorized(ApiResponseDto<bool>.Fail("Unauthorized"));
        }

        if (request == null || request.OrderCode <= 0)
        {
            return BadRequest(ApiResponseDto<bool>.Fail("Order code is required."));
        }

        var result = await _paymentService.CancelPackagePurchaseAsync(request.OrderCode, buyerId);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
