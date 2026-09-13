using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class Friendship
{
    public Guid RequesterId { get; set; }

    public Guid AddresseeId { get; set; }

    public string? Status { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User Addressee { get; set; } = null!;

    public virtual User Requester { get; set; } = null!;
}
