using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class VideoCall
{
    public Guid MessageId { get; set; }

    public int? DurationSeconds { get; set; }

    public string? CallStatus { get; set; }

    public virtual Message Message { get; set; } = null!;
}
