namespace FitSocial.Client.Models.Ekyc;

public class EkycRequest
{
    public string FrontCardUrl { get; set; } = string.Empty;
    public string BackCardUrl { get; set; } = string.Empty;
    public string? FaceImageUrl { get; set; }
    public string? FaceImageLeftUrl { get; set; }
    public string? FaceImageRightUrl { get; set; }
    public string? Email { get; set; }
}

public class EkycResult
{
    public string IdCardNumber { get; set; } = string.Empty;
    public string FullNameOnCard { get; set; } = string.Empty;
    public DateOnly? DateOfBirthOnCard { get; set; }
    public string VerificationStatus { get; set; } = string.Empty;
}
