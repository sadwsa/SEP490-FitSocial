using System;

namespace FitSocial.Application.DTOs.AuditLogs;

public class GetAuditLogsQueryDto
{
    public string? Search { get; set; }
    public string? Action { get; set; }
    public string? EntityType { get; set; }
    public Guid? ActorId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
