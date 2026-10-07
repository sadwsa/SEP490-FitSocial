using System;
using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Users;

public class UpdateOwnProfileRequestDto
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 100 characters.")]
    [RegularExpression(@"^[^\d]+$", ErrorMessage = "Full name cannot contain numbers.")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(2048, ErrorMessage = "Avatar URL must not exceed 2048 characters.")]
    public string? AvatarUrl { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [StringLength(6, ErrorMessage = "Gender must not exceed 6 characters.")]
    public string? Gender { get; set; }

    [RegularExpression(@"^0\d{9}$", ErrorMessage = "Phone number must be a valid 10-digit number starting with 0.")]
    public string? PhoneNumber { get; set; }

    [StringLength(2000, ErrorMessage = "Biography must not exceed 2000 characters.")]
    public string? Bio { get; set; }

    [Range(0, 60, ErrorMessage = "Years of experience must be between 0 and 60.")]
    public int? ExperienceYears { get; set; }
}
