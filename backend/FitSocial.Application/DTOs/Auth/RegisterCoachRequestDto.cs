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

    /// <summary>Coaching specialties (Sports table, multiple allowed) -> saved to CoachSports.</summary>
    [MinLength(1, ErrorMessage = "Please select at least one specialty")]
    public List<Guid>? SpecialtySportIds { get; set; }

    /// <summary>Years of coaching experience -> CoachProfiles.ExperienceYears.</summary>
    [Required(ErrorMessage = "Please enter your years of experience")]
    [Range(0, 60, ErrorMessage = "Years of experience must be between 0 and 60")]
    public int? ExperienceYears { get; set; }

    /// <summary>Short professional biography -> CoachProfiles.Bio.</summary>
    [StringLength(2000, ErrorMessage = "Biography must be under 2000 characters")]
    public string? Biography { get; set; }

    /// <summary>Link to certificates/credentials -> CoachProfiles.CertificateUrl.</summary>
    [StringLength(2048, ErrorMessage = "Certificate link is too long")]
    public string? CertificateUrl { get; set; }

    /// <summary>Link to the identity card image -> CoachProfiles.IdentityCardUrl.</summary>
    [StringLength(2048, ErrorMessage = "Identity card link is too long")]
    public string? IdentityCardUrl { get; set; }

    public List<Guid>? FavoriteSportIds { get; set; }
}
