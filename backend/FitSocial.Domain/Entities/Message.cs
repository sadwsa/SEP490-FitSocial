using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class Message
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public Guid SenderId { get; set; }

    public string? Content { get; set; }

    public string? MessageType { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;

    public virtual User Sender { get; set; } = null!;

    public virtual VideoCall? VideoCall { get; set; }
}
