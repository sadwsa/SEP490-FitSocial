using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class Conversation
{
    public Guid ConversationId { get; set; }

    public Guid? User1Id { get; set; }

    public Guid? User2Id { get; set; }

    public string? LastMessageContent { get; set; }

    public Guid? LastMessageSenderId { get; set; }

    public DateTime? LastMessageAt { get; set; }

    public DateTime? User1DeletedHistoryAt { get; set; }

    public DateTime? User2DeletedHistoryAt { get; set; }

    public Guid? User1LastReadMessageId { get; set; }

    public Guid? User2LastReadMessageId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    // Helper property for backward compatibility with Id
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public Guid Id
    {
        get => ConversationId;
        set => ConversationId = value;
    }

    public virtual User? User1 { get; set; }

    public virtual User? User2 { get; set; }

    public virtual User? LastMessageSender { get; set; }

    public virtual Message? User1LastReadMessage { get; set; }

    public virtual Message? User2LastReadMessage { get; set; }

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
}
