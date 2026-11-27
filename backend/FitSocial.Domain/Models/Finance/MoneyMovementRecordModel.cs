using System;

namespace FitSocial.Domain.Models.Finance;

public class MoneyMovementRecordModel
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