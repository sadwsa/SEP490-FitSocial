using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class UserBlock
{
    public Guid Id { get; set; }

    public Guid BlockerId { get; set; }

    public Guid BlockedId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User Blocked { get; set; } = null!;

    public virtual User Blocker { get; set; } = null!;
}
