using System.ComponentModel.DataAnnotations;
using FitSocial.Application.DTOs.Auth;

namespace FitSocial.Application.DTOs.Payments;

public class ConfirmCoachActivationRequestDto : RegisterCoachRequestDto
{
    /// <summary>Gateway transaction reference. Refs starting with "FAIL" simulate a failed payment (mock).</summary>
    [Required(ErrorMessage = "Missing payment transaction reference")]
    public string TransactionRef { get; set; } = string.Empty;

    /// <summary>Payment method label (VNPay, MoMo, ATM Card...).</summary>
    public string? Method { get; set; }
}
