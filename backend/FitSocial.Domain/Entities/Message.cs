using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class Message
{
    public Guid MessageId { get; set; }

    public Guid ConversationId { get; set; }

    public Guid SenderId { get; set; }

    public string? Content { get; set; }

    public string? MessageType { get; set; }

    public DateTime? CreatedAt { get; set; }

    // Helper property for backward compatibility with Id
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public Guid Id
    {
        get => MessageId;
        set => MessageId = value;
    }

    public virtual Conversation Conversation { get; set; } = null!;

    public virtual User Sender { get; set; } = null!;

    public virtual ICollection<MessageAttachment> MessageAttachments { get; set; } = new List<MessageAttachment>();
}
