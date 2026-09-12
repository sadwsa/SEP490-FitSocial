using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class PostInteraction
{
    public Guid PostId { get; set; }

    public Guid UserId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Post Post { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
