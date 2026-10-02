using System;

namespace FitSocial.Domain.Entities;

public partial class MessageAttachment
{
    public Guid AttachmentId { get; set; }

    public Guid MessageId { get; set; }

    public string? MediaUrl { get; set; }

    public string? ThumbnailUrl { get; set; }

    public string? MediaType { get; set; }

    public long? FileSize { get; set; }

    public int? DurationSeconds { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Message Message { get; set; } = null!;
}
