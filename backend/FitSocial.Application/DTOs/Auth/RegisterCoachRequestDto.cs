using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Auth;

public class RegisterCoachRequestDto
{
    [Required(ErrorMessage = "Full name is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be 2 to 100 characters")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter the OTP code")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP code must be 6 digits")]
    public string OtpCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Contact phone number is required for coaches")]
    [Phone(ErrorMessage = "Invalid phone number format")]
    public string? PhoneNumber { get; set; }

    public string? Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [Required(ErrorMessage = "Please enter your years of experience")]
    [Range(0, 60, ErrorMessage = "Years of experience must be between 0 and 60")]
    public int? ExperienceYears { get; set; }

    [StringLength(2000, ErrorMessage = "Biography must be under 2000 characters")]
    public string? Biography { get; set; }

    [StringLength(2048, ErrorMessage = "Certificate link is too long")]
    public string? CertificateUrl { get; set; }

    [StringLength(2048, ErrorMessage = "Identity card link is too long")]
    public string? IdentityCardUrl { get; set; }

    // --- 5-step flow (all data comes from frontend, no mock) ---
    /// <summary>Version of terms the user agreed to (TermsAndPolicies.TermID).</summary>
    public Guid? TermId { get; set; }

    /// <summary>eKYC front/back/face image URLs (uploaded via /upload, verified before payment).</summary>
    public string? FrontCardUrl { get; set; }
    public string? BackCardUrl { get; set; }
    public string? FaceImageUrl { get; set; }
    /// <summary>Optional extra liveness poses: Left/Right/Top/Bottom (lại gần/ra xa được map vào Portrait, trái/phải vào Left/Right)</summary>
    public string? FaceImageLeftUrl { get; set; }
    public string? FaceImageRightUrl { get; set; }
    public string? FaceImageTopUrl { get; set; }
    public string? FaceImageBottomUrl { get; set; }

    /// <summary>Multiple certificates for the coach (replaces single CertificateUrl).</summary>
    public List<FitSocial.Application.DTOs.Coach.CoachCertificateDto>? Certificates { get; set; }
}
