using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class AppealRepository : Repository<Appeal>, IAppealRepository
{
    public AppealRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<Appeal?> GetByIdWithDetailsAsync(Guid appealId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(a => a.Appealer)
            .Include(a => a.Report)
                .ThenInclude(r => r.ReportedPost)
            .Include(a => a.Report)
                .ThenInclude(r => r.Reporter)
            .Include(a => a.ReviewedByNavigation)
            .Include(a => a.AppealMedia)
            .FirstOrDefaultAsync(a => a.AppealId == appealId, cancellationToken);
    }

    public async Task<Appeal?> GetByReportIdAsync(Guid reportId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(a => a.Appealer)
            .Include(a => a.Report)
            .Include(a => a.ReviewedByNavigation)
            .Include(a => a.AppealMedia)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(a => a.ReportId == reportId, cancellationToken);
    }

    public async Task<List<Appeal>> GetAppealsByAppealerIdAsync(Guid appealerId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Include(a => a.Report)
            .Include(a => a.AppealMedia)
            .Include(a => a.ReviewedByNavigation)
            .Where(a => a.AppealerId == appealerId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasActiveAppealForReportAsync(Guid reportId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AnyAsync(a => a.ReportId == reportId && (a.Status == "PENDING" || a.Status == "APPROVED"), cancellationToken);
    }
}
