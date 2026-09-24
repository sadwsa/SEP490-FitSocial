using System;

namespace FitSocial.Application.DTOs.Conversations;

public class MessageDto
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string? SenderName { get; set; }
    public string? SenderAvatar { get; set; }
    public string? Content { get; set; }
    public string? MessageType { get; set; }
    public DateTime? CreatedAt { get; set; }
    public bool IsMine { get; set; }
    public MessageDto? AutoReply { get; set; }
}
