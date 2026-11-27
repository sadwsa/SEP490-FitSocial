using System.ComponentModel.DataAnnotations;

namespace FitSocial.Client.Models.Coaches;

public class CreateCoachBankAccountDto
{
    [Required(ErrorMessage = "Bank name is required")]
    [StringLength(255, ErrorMessage = "Bank name cannot exceed 255 characters")]
    public string BankName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Bank code is required")]
    [StringLength(50, ErrorMessage = "Bank code cannot exceed 50 characters")]
    public string BankCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Account name is required")]
    [StringLength(255, ErrorMessage = "Account name cannot exceed 255 characters")]
    public string AccountName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Account number is required")]
    [RegularExpression(@"^[\d\s-]{6,30}$", ErrorMessage = "Account number must contain 6-20 digits")]
    public string AccountNumber { get; set; } = string.Empty;

    [StringLength(255, ErrorMessage = "Branch cannot exceed 255 characters")]
    public string? Branch { get; set; }

    public bool IsDefault { get; set; }
}
