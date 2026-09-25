using FitSocial.Domain.Enums;

namespace FitSocial.Domain.Entities;

public partial class Report
{
    /// <summary>
    /// Factory method to create a new pending post report.
    /// </summary>
    public static Report CreatePostReport(Guid reporterId, Guid postId, Guid reportedAuthorId, string reason)
    {
        var now = DateTime.UtcNow;
        return new Report
        {
            ReportId = Guid.NewGuid(),
            ReporterId = reporterId,
            ReportedPostId = postId,
            ReportedUserId = reportedAuthorId,
            Reason = reason.Trim(),
            Status = ReportStatus.Pending.ToString(),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Checks if this report is currently active/pending.
    /// </summary>
    public bool IsPending()
    {
        return string.Equals(Status, ReportStatus.Pending.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
