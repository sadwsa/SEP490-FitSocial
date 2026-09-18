using System;
using System.Collections.Generic;

namespace FitSocial.Application.DTOs.Conversations;

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
    public List<MessageDto> Messages { get; set; } = new();
}
