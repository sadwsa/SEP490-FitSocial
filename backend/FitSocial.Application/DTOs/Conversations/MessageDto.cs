using System;
using System.Collections.Generic;

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
    public List<MessageAttachmentDto> Attachments { get; set; } = new();
    public MessageDto? AutoReply { get; set; }
}

public class MessageAttachmentDto
{
    public Guid AttachmentId { get; set; }
    public string? MediaUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? MediaType { get; set; }
    public long? FileSize { get; set; }
    public int? DurationSeconds { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
}
