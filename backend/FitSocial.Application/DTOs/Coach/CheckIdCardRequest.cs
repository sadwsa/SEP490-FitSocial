namespace FitSocial.Application.DTOs.Coach;

/// <summary>
/// Check if a CCCD/ID card has already been used by another coach.
/// Called right after OCR so duplicates surface immediately on the CCCD page.
/// </summary>
public record CheckIdCardRequest(string IdCardNumber, string? Email);
