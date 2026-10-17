using System;

namespace FitSocial.Domain.Models.Finance;

/// <summary>
/// Domain-level projection of one movement of money leaving the platform,
/// unifying coach payout lines and trainee refund requests.
/// </summary>
public class MoneyMovementRecordModel
{
    public Guid Id { get; set; }

    /// <summary>PAYOUT (money to coach) or REFUND (money to trainee).</summary>
    public string MovementType { get; set; } = string.Empty;

    /// <summary>COACH or TRAINEE.</summary>
    public string RecipientRole { get; set; } = string.Empty;

    public Guid RecipientId { get; set; }

    public string? RecipientName { get; set; }

    public string? RecipientEmail { get; set; }

    public string? RecipientAvatarUrl { get; set; }

    public decimal Amount { get; set; }

    public string? Status { get; set; }

    /// <summary>Settlement cycle for payouts, e.g. "10/2026". Null for refunds.</summary>
    public string? CycleLabel { get; set; }

    /// <summary>Reason supplied on a refund request. Null for payouts.</summary>
    public string? Note { get; set; }

    public DateTime? OccurredOn { get; set; }

    public DateTime? RequestedOn { get; set; }

    public string? TransactionRef { get; set; }
}