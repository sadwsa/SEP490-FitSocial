using System;

namespace FitSocial.Application.DTOs.Reports;

public class GetReportsQueryDto
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Status { get; set; }
    public string? TargetType { get; set; }
    public Guid? ReporterId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SearchTerm { get; set; }
}
