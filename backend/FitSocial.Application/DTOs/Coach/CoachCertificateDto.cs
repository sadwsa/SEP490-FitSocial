namespace FitSocial.Application.DTOs.Coach;

public class CoachCertificateDto
{
    public string? CertificateName { get; set; }
    public string CertificateUrl { get; set; } = string.Empty;
    public DateOnly? IssuedDate { get; set; }
}
