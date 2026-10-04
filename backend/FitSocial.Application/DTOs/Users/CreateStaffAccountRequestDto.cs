using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Users;

public class CreateStaffAccountRequestDto
{
    [Required(ErrorMessage = "Full name is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be 2 to 100 characters")]
    [RegularExpression(@"^[^\d]+$", ErrorMessage = "Full name cannot contain numbers")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(100, ErrorMessage = "Email must not exceed 100 characters")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [RegularExpression(@"^0\d{9}$", ErrorMessage = "Phone number must be a valid 10-digit number starting with 0")]
    public string? PhoneNumber { get; set; }
}
