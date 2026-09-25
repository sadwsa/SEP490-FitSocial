using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface IPostReportRepository : IRepository<Report>
{
    /// <summary>
    /// Checks whether an active/pending report exists specifically for the given post and reporter.
    /// Scoped strictly to postId + reporterId, ensuring reports on other posts from the same author do NOT conflict.
    /// </summary>
    Task<bool> HasActiveReportAsync(Guid postId, Guid reporterId, CancellationToken cancellationToken = default);

    Task<Report?> GetActiveReportAsync(Guid postId, Guid reporterId, CancellationToken cancellationToken = default);
}
