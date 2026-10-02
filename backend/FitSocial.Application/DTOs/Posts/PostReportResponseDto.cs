using System;
using System.Collections.Generic;

namespace FitSocial.Application.DTOs.Posts;

public class PostReportResponseDto
{
    public Guid ReportId { get; set; }
    public Guid PostId { get; set; }
    public Guid ReporterId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Type { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<string> MediaUrls { get; set; } = new();
    public DateTime? CreatedAt { get; set; }
}
