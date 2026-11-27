namespace FitSocial.Application.DTOs.Coach;

public class EkycVerificationDto
{
    public string FrontCardUrl { get; set; } = string.Empty;

    public string BackCardUrl { get; set; } = string.Empty;
 
    public string? FaceImageUrl { get; set; }

    public string? FaceImageLeftUrl { get; set; }

    public string? FaceImageRightUrl { get; set; }

    public string? Email { get; set; }

    public Guid? UserId { get; set; }
}
