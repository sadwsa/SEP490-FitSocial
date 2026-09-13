namespace FitSocial.Application.DTOs.Payments;

public class CoachActivationPreviewDto
{
    public string Email { get; set; } = string.Empty;
    public decimal AmountVnd { get; set; }
    public string Currency { get; set; } = "VND";
    public string OrderType { get; set; } = "COACH_ACTIVATION";
}
