namespace FitSocial.Application.DTOs.Coach;

public class CoachCertificateDto
{
    public string? CertificateName { get; set; }
    public string CertificateUrl { get; set; } = string.Empty;
    public string? IssuedBy { get; set; }
    public DateOnly? IssuedDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
}
