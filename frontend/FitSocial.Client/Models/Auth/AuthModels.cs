using System.ComponentModel.DataAnnotations;

namespace FitSocial.Client.Models.Auth;

public class SendOtpRequest
{
    [Required(ErrorMessage = "Please enter your email")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public string Email { get; set; } = string.Empty;

    public string? Purpose { get; set; } = "REGISTER_TRAINEE";
}

public class RegisterRequest
{
    [Required(ErrorMessage = "Please enter your full name")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be 2 to 100 characters")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your email")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter a password")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter the 6-digit OTP code")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP code must be 6 digits")]
    public string OtpCode { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Invalid phone number format")]
    public string? PhoneNumber { get; set; }

    public string? Gender { get; set; } = "MALE";

    public DateOnly? DateOfBirth { get; set; }

    /// <summary>
    /// Danh sách ID môn thể thao yêu thích (chọn nhiều), gửi lên API để lưu vào UserFavoriteSports.
    /// </summary>
    public List<Guid> FavoriteSportIds { get; set; } = new();

    // ---- Coach-only fields (sent to register-coach) ----
    /// <summary>Primary coaching specialty -> CoachSports.</summary>
    public List<Guid> SpecialtySportIds { get; set; } = new();

    /// <summary>Years of coaching experience -> CoachProfiles.ExperienceYears.</summary>
    public int? ExperienceYears { get; set; }

    /// <summary>Short professional biography -> CoachProfiles.Bio.</summary>
    public string? Biography { get; set; }

    /// <summary>Link to certificates/credentials -> CoachProfiles.CertificateUrl.</summary>
    public string? CertificateUrl { get; set; }

    /// <summary>Link to the identity card image -> CoachProfiles.IdentityCardUrl.</summary>
    public string? IdentityCardUrl { get; set; }
}


public class LoginRequest
{
    [Required(ErrorMessage = "Vui lòng nhập Email")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập Mật khẩu")]
    [MinLength(6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
    public string Password { get; set; } = string.Empty;
}

public class GoogleLoginRequest
{
    public string IdToken { get; set; } = string.Empty;

    /// <summary>
    /// Desired role for newly created accounts only ("TRAINEE" or "COACH").
    /// </summary>
    public string? RoleCode { get; set; }
}

public class AuthResponse
{    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserInfo User { get; set; } = new();
}

public class UserInfo
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? RoleCode { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public List<string> Roles { get; set; } = new();
}
