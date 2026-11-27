using System;

namespace FitSocial.Client.Models.SystemLogs;

public class SystemLogItemModel
{
    public long LogId { get; set; }
    public string LogIdDisplay { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "Information";
    public string ActionType { get; set; } = "UPDATE";
    public string Status { get; set; } = "Success";
    public string Source { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? ActorId { get; set; }
    public string? ActorName { get; set; }
    public string? ActorEmail { get; set; }
    public string? ActorRole { get; set; }
    public string? IpAddress { get; set; }
    public string? EntityId { get; set; }
    public string? TargetDisplayName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Details { get; set; }
}

public class SystemLogSummaryModel
{
    public int TotalActivities { get; set; }
    public int AdminActivities { get; set; }
    public int StaffActivities { get; set; }
    public int FailedActivities { get; set; }
}

public class GetSystemLogsRequest
{
    public string? Search { get; set; }
    public string? Role { get; set; }
    public string? ActionType { get; set; }
    public string? Status { get; set; }
    public string? Level { get; set; }
    public string? Source { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SortOrder { get; set; } = "desc";
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
