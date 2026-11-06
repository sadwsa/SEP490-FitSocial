using System;

namespace FitSocial.Application.DTOs.SystemLogs;

public class GetSystemLogsQueryDto
{
    public string? Search { get; set; }
    public string? Level { get; set; }
    public string? Source { get; set; }
    public string? Role { get; set; }
    public string? ActionType { get; set; }
    public string? Status { get; set; }
    public string? SortOrder { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
