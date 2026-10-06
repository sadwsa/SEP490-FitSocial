namespace FitSocial.Client.Models.BankAccounts;

/// <summary>
/// UC-36: one row of the admin payment accounts list (masked number only).
/// </summary>
public class CoachPaymentAccount
{
    public Guid BankId { get; set; }
    public Guid CoachId { get; set; }
    public string CoachName { get; set; } = string.Empty;
    public string? CoachEmail { get; set; }
    public string? BankName { get; set; }
    public string? BankCode { get; set; }
    public string? AccountName { get; set; }
    public string MaskedAccountNumber { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string? Branch { get; set; }
    public bool IsDefault { get; set; }
    public DateTime? CreatedAt { get; set; }
}
