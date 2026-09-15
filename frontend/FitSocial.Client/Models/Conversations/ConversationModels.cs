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
