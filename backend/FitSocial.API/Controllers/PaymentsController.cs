using FitSocial.Application.DTOs.Auth;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Payments;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
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

        var result = await _paymentService.CreateActivationLinkAsync(request, originUrl ?? string.Empty);
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
    /// Matches the payment by order code and unlocks the account when paid.
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

            await _paymentService.CompleteActivationAsync(orderCode);
            return Ok(new { success = true });
        }
        catch
        {
            // Never fail the webhook handshake because of our errors
            return Ok(new { success = false });
        }
    }
}
