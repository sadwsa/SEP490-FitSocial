using System;
using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Coach;

public class CoachBankAccountDto
{
    public Guid BankId { get; set; }
    public Guid CoachId { get; set; }
    public string? BankName { get; set; }
    public string? BankCode { get; set; }
    public string? AccountName { get; set; }
    public string? AccountNumber { get; set; }
    public string? MaskedAccountNumber { get; set; }
    public string? Branch { get; set; }
    public bool? IsDefault { get; set; }
    public bool? IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateCoachBankAccountDto
{
    [Required]
    [StringLength(100)]
    public string BankName { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string BankCode { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string AccountName { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 6)]
    public string AccountNumber { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Branch { get; set; }

    public bool IsDefault { get; set; } = false;
}
