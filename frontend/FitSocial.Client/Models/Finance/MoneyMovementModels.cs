using System;
using System.Collections.Generic;

namespace FitSocial.Client.Models.Finance;

public class MoneyMovementDto
{
    public Guid Id { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public string RecipientRole { get; set; } = string.Empty;
    public Guid RecipientId { get; set; }
    public string? RecipientName { get; set; }
    public string? RecipientEmail { get; set; }
    public string? RecipientAvatarUrl { get; set; }
    public decimal Amount { get; set; }
    public string? Status { get; set; }
    public string? CycleLabel { get; set; }
    public string? Note { get; set; }
    public DateTime? OccurredOn { get; set; }
    public DateTime? RequestedOn { get; set; }
    public string? TransactionRef { get; set; }
}

public class MoneyMovementListDto
{
    public List<MoneyMovementDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public decimal TotalPayoutAmount { get; set; }
    public decimal TotalRefundAmount { get; set; }
}