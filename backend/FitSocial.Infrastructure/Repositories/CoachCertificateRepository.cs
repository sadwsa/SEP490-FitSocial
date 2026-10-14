using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class CoachCertificateRepository : Repository<CoachCertificate>, ICoachCertificateRepository
{
    public CoachCertificateRepository(FitSocialDbContext dbContext) : base(dbContext) { }

    public override void Update(CoachCertificate entity)
    {
        var entry = DbContext.Entry(entity);
        if (entry.State == EntityState.Detached)
        {
            DbSet.Update(entity);
        }
    }

    public Task<List<CoachCertificate>> ListByCoachIdAsync(Guid coachId, CancellationToken cancellationToken = default)
    {
        return DbSet.Where(c => c.CoachId == coachId).ToListAsync(cancellationToken);
    }

    public async Task<(List<CoachCertificate> Items, int TotalCount)> GetPagedCertificatesAsync(
        string? search = null,
        string? status = null,
        string? sortBy = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .Include(c => c.Coach)
                .ThenInclude(p => p.Coach)
            .Include(c => c.VerifiedByNavigation)
            .AsNoTracking();

        // 1. Status Filter
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            var st = status.Trim().ToUpperInvariant();
            if (st == "PENDING")
            {
                query = query.Where(c => c.VerificationStatus == null || c.VerificationStatus.ToUpper() == "PENDING");
            }
            else if (st == "APPROVED")
            {
                query = query.Where(c => c.VerificationStatus != null && c.VerificationStatus.ToUpper() == "APPROVED");
            }
            else if (st == "REJECTED")
            {
                query = query.Where(c => c.VerificationStatus != null && c.VerificationStatus.ToUpper() == "REJECTED");
            }
        }

        // 2. Search
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLower();
            query = query.Where(c =>
                (c.CertificateName != null && c.CertificateName.ToLower().Contains(keyword)) ||
                (c.IssuedBy != null && c.IssuedBy.ToLower().Contains(keyword)) ||
                (c.Coach != null && c.Coach.Coach != null && c.Coach.Coach.FullName != null && c.Coach.Coach.FullName.ToLower().Contains(keyword)) ||
                (c.Coach != null && c.Coach.Coach != null && c.Coach.Coach.Email != null && c.Coach.Coach.Email.ToLower().Contains(keyword)));
        }

        // 3. Sorting
        query = (sortBy?.Trim().ToLowerInvariant()) switch
        {
            "date_asc" => query.OrderBy(c => c.CreatedAt ?? DateTime.MinValue),
            "name_asc" => query.OrderBy(c => c.CertificateName ?? string.Empty),
            "name_desc" => query.OrderByDescending(c => c.CertificateName ?? string.Empty),
            "coach_asc" => query.OrderBy(c => c.Coach.Coach.FullName ?? string.Empty),
            "coach_desc" => query.OrderByDescending(c => c.Coach.Coach.FullName ?? string.Empty),
            _ => query.OrderByDescending(c => c.CreatedAt ?? DateTime.MinValue)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<Dictionary<string, int>> GetStatusCountsAsync(CancellationToken cancellationToken = default)
    {
        var allCertificates = await DbSet.AsNoTracking().ToListAsync(cancellationToken);

        var pending = allCertificates.Count(c => string.IsNullOrWhiteSpace(c.VerificationStatus) ||
                                                 string.Equals(c.VerificationStatus, "PENDING", StringComparison.OrdinalIgnoreCase));
        var approved = allCertificates.Count(c => string.Equals(c.VerificationStatus, "APPROVED", StringComparison.OrdinalIgnoreCase));
        var rejected = allCertificates.Count(c => string.Equals(c.VerificationStatus, "REJECTED", StringComparison.OrdinalIgnoreCase));

        return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["ALL"] = allCertificates.Count,
            ["PENDING"] = pending,
            ["APPROVED"] = approved,
            ["REJECTED"] = rejected
        };
    }

    public Task<CoachCertificate?> GetCertificateWithDetailsAsync(Guid certificateId, CancellationToken cancellationToken = default)
    {
        return DbSet
            .Include(c => c.Coach)
                .ThenInclude(p => p.Coach)
            .Include(c => c.VerifiedByNavigation)
            .FirstOrDefaultAsync(c => c.CertificateId == certificateId, cancellationToken);
    }
}
