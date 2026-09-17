using System;

namespace FitSocial.Client.Models.Notifications;

public class NotificationDto
{
    public Guid NotificationId { get; set; }
    public string Type { get; set; } = "NewMessage";
    public Guid SenderId { get; set; }
    public string? SenderName { get; set; }
    public string? SenderAvatar { get; set; }
    public Guid ConversationId { get; set; }
    public Guid? MessageId { get; set; }
    public string? MessagePreview { get; set; }
    public DateTime? CreatedAt { get; set; }
    public bool IsRead { get; set; }
}
