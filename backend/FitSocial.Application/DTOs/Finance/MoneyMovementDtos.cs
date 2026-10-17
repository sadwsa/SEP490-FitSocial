using System;

namespace FitSocial.Application.DTOs.Finance;

/// <summary>
/// A single movement of money leaving the platform towards a user.
/// Unifies coach payouts and trainee refunds into one chronological ledger.
/// </summary>
public class MoneyMovementDto
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

    /// <summary>When the money actually left the platform (falls back to record date).</summary>
    public DateTime? OccurredOn { get; set; }

    public DateTime? RequestedOn { get; set; }

    public string? TransactionRef { get; set; }
}

public class MoneyMovementQueryDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    /// <summary>PAYOUT, REFUND, or null for all.</summary>
    public string? MovementType { get; set; }

    /// <summary>COACH, TRAINEE, or null for all.</summary>
    public string? RecipientRole { get; set; }

    public string? Status { get; set; }

    public string? SearchTerm { get; set; }

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class MoneyMovementListDto
{
    public List<MoneyMovementDto> Items { get; set; } = new();
    public int TotalCount { get; set; }

    public decimal TotalPayoutAmount { get; set; }
    public decimal TotalRefundAmount { get; set; }
}