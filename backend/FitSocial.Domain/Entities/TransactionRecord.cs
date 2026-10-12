using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace FitSocial.Domain.Entities;

[NotMapped]
public class TransactionRecord
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
