using System;

namespace FitSocial.Application.DTOs.AuditLogs;

public class CreateAuditLogDto
{
    public Guid? ActorAccountId { get; set; }
    public string? ActorRole { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Description { get; set; }
    public object? OldValue { get; set; }
    public object? NewValue { get; set; }
    public string? IpAddress { get; set; }
}
