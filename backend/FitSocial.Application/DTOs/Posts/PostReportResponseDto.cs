using System;

namespace FitSocial.Application.DTOs.Posts;

public class PostReportResponseDto
{
    public Guid ReportId { get; set; }
    public Guid PostId { get; set; }
    public Guid ReporterId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
}
