using System;

namespace FitSocial.Application.DTOs.AuditLogs;

public class AuditLogDto
{
    public long AuditLogId { get; set; }
    public Guid? ActorAccountId { get; set; }
    public string? ActorName { get; set; }
    public string? ActorEmail { get; set; }
    public string? ActorRole { get; set; }
    public string? Action { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Description { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? IpAddress { get; set; }
    public DateTime? CreatedAt { get; set; }
}
