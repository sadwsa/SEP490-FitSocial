using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class Participant
{
    public Guid ConversationId { get; set; }

    public Guid UserId { get; set; }

    public DateTime? JoinedAt { get; set; }

    public Guid? LastReadMessageId { get; set; }

    public DateTime? HistoryDeletedAt { get; set; }

    public string? Status { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
