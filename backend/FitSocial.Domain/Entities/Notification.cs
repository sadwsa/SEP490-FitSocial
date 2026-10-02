using System;

namespace FitSocial.Domain.Entities;

public partial class Notification
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid? ActorId { get; set; }

    public Guid? ReferenceId { get; set; }

    public string? Type { get; set; }

    public string? Description { get; set; }

    public bool? IsRead { get; set; } = false;

    public DateTime? CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual User? Actor { get; set; }
}
