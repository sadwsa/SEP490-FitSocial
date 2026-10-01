using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Conversations;

public class SendMessageRequestDto
{
    [MaxLength(1000, ErrorMessage = "Message cannot exceed 1000 characters.")]
    public string? Content { get; set; }

    public string? MessageType { get; set; } = "TEXT";

    public List<CreateMessageAttachmentDto>? Attachments { get; set; }
}

public class CreateMessageAttachmentDto
{
    [Required(ErrorMessage = "MediaUrl is required.")]
    public string MediaUrl { get; set; } = string.Empty;

    public string? ThumbnailUrl { get; set; }

    public string? MediaType { get; set; } // "IMAGE", "VIDEO", "FILE", etc.

    public long? FileSize { get; set; }

    public int? DurationSeconds { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }
}
