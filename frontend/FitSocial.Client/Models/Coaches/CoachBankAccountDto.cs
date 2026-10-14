using System;

namespace FitSocial.Client.Models.Coaches;

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
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
