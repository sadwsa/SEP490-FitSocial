using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface IPostReportRepository : IRepository<Report>
{
    /// <summary>
    /// Checks if a reporter has already submitted an active/pending report for the specified post.
    /// </summary>
    Task<bool> HasActiveReportAsync(Guid postId, Guid reporterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an active/pending report by post and reporter.
    /// </summary>
    Task<Report?> GetActiveReportAsync(Guid postId, Guid reporterId, CancellationToken cancellationToken = default);
}
