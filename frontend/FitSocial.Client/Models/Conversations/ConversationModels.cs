using System;

namespace FitSocial.Client.Models.Conversations;

public class ConversationDto
{
    public Guid ConversationId { get; set; }
    public string? Type { get; set; }
    public string? Title { get; set; }
    public string? DisplayAvatar { get; set; }
    public Guid? OtherUserId { get; set; }
    public string? OtherUserName { get; set; }
    public string? OtherUserAvatar { get; set; }
    public string? LastMessage { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public Guid? LastMessageSenderId { get; set; }
    public int UnreadCount { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

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

public class ConversationDetailDto
{
    public Guid ConversationId { get; set; }
    public string? Type { get; set; } // "DIRECT" or "GROUP"
    public string? Title { get; set; }
    public string? DisplayAvatar { get; set; }
    public Guid? OtherUserId { get; set; }
    public string? OtherUserName { get; set; }
    public string? OtherUserAvatar { get; set; }
    public DateTime? CreatedAt { get; set; }
    public bool IsBlockedByMe { get; set; }
    public bool IsBlockedByOther { get; set; }
    public List<MessageDto> Messages { get; set; } = new();
}

public class SendMessageRequestDto
{
    public string? Content { get; set; }
    public string? MessageType { get; set; } = "TEXT";
    public List<CreateMessageAttachmentDto>? Attachments { get; set; }
}

public class CreateMessageAttachmentDto
{
    public string MediaUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? MediaType { get; set; }
    public long? FileSize { get; set; }
    public int? DurationSeconds { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
}
