using System;

namespace FitSocial.Client.Models.Transactions;

public class TransactionHistoryDto
{
    public Guid TransactionId { get; set; }
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ReferenceId { get; set; } = string.Empty;
}
