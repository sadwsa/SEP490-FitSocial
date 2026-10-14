using System.ComponentModel.DataAnnotations;

namespace FitSocial.Client.Models.Coaches;

public class CreateCoachBankAccountDto
{
    [Required(ErrorMessage = "Bank name is required")]
    public string BankName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Bank code is required")]
    public string BankCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Account name is required")]
    public string AccountName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Account number is required")]
    public string AccountNumber { get; set; } = string.Empty;

    public string? Branch { get; set; }

    public bool IsDefault { get; set; }
}
