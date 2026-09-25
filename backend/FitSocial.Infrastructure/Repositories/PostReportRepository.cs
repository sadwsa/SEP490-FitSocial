using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Enums;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class PostReportRepository : Repository<Report>, IPostReportRepository
{
    public PostReportRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    /// <summary>
    /// Checks whether an active/pending report exists specifically for the given post and reporter.
    /// Scoped strictly to ReportedPostId == postId and ReporterId == reporterId.
    /// This ensures reports of different posts from the same user are NOT treated as duplicates.
    /// </summary>
    public async Task<bool> HasActiveReportAsync(Guid postId, Guid reporterId, CancellationToken cancellationToken = default)
    {
        var pendingStatus = ReportStatus.Pending.ToString();
        return await DbSet.AnyAsync(r =>
            r.ReportedPostId == postId &&
            r.ReporterId == reporterId &&
            r.Status != null &&
            r.Status.ToLower() == pendingStatus.ToLower(),
            cancellationToken);
    }

    public async Task<Report?> GetActiveReportAsync(Guid postId, Guid reporterId, CancellationToken cancellationToken = default)
    {
        var pendingStatus = ReportStatus.Pending.ToString();
        return await DbSet.FirstOrDefaultAsync(r =>
            r.ReportedPostId == postId &&
            r.ReporterId == reporterId &&
            r.Status != null &&
            r.Status.ToLower() == pendingStatus.ToLower(),
            cancellationToken);
    }
}
