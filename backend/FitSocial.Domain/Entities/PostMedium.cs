using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class PostMedium
{
    public Guid Id { get; set; }

    public Guid PostId { get; set; }

    public string? MediaUrl { get; set; }

    public string? MediaType { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Post Post { get; set; } = null!;
}
