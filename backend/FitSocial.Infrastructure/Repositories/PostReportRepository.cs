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
