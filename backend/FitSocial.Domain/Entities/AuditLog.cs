using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class AuditLog
{
    public long AuditLogId { get; set; }

    public Guid? ActorAccountId { get; set; }

    public string? Action { get; set; }

    public string? EntityType { get; set; }

    public string? EntityId { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string? Ipaddress { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User? ActorAccount { get; set; }
}
