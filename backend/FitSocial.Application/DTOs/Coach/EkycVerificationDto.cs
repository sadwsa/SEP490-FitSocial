namespace FitSocial.Application.DTOs.Coach;

public class EkycVerificationDto
{
    public string FrontCardUrl { get; set; } = string.Empty;
    public string BackCardUrl { get; set; } = string.Empty;
    /// <summary>Portrait (chính diện) - bắt buộc</summary>
    public string? FaceImageUrl { get; set; }
    /// <summary>Các pose phụ cho liveness: Left/Right/Top/Bottom hoặc near/far (nếu có)</summary>
    public string? FaceImageLeftUrl { get; set; }
    public string? FaceImageRightUrl { get; set; }
    public string? FaceImageTopUrl { get; set; }
    public string? FaceImageBottomUrl { get; set; }
}
