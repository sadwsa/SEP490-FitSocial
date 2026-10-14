using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class CoachProfileRepository : Repository<CoachProfile>, ICoachProfileRepository
{
    public CoachProfileRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public Task<CoachProfile?> FindWithDetailsByCoachIdAsync(Guid coachId, CancellationToken cancellationToken = default)
    {
        return DbSet
            .Include(p => p.Coach)
            .Include(p => p.Locations)
            .Include(p => p.CoachCertificates)
            .Include(p => p.Reviews)
            .Include(p => p.TrainingPackages)
            .FirstOrDefaultAsync(p => p.CoachId == coachId, cancellationToken);
    }

    public async Task<List<CoachRatingSummary>> GetTopCoachesAsync(int count, CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking()
            .Where(cp => (cp.Coach.IsLocked == null || cp.Coach.IsLocked == false)
                      && cp.Coach.RoleCode != null
                      && cp.Coach.RoleCode.ToUpper() == RoleConstants.Coach
                      && (cp.ApprovalStatus == null || cp.ApprovalStatus.ToUpper() == "APPROVED"))
            .Select(cp => new CoachRatingSummary
            {
                CoachId = cp.CoachId,
                FullName = cp.Coach.FullName ?? string.Empty,
                AvatarUrl = cp.Coach.AvatarUrl,
                Bio = cp.Bio,
                ExperienceYears = cp.ExperienceYears,
                TotalReviews = cp.Reviews.Count(r => r.Rating != null),
                AverageRating = cp.Reviews.Where(r => r.Rating != null).Select(r => (double?)r.Rating).Average() ?? 0.0
            })
            .OrderByDescending(c => c.AverageRating)
            .ThenByDescending(c => c.TotalReviews)
            .ThenBy(c => c.CoachId)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<CoachProfile>> GetAllCoachesWithDetailsAsync(string? searchKeyword = null, int? minExperience = null, string? sortBy = null, CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .Include(c => c.Coach)
            .Include(c => c.Locations)
            .AsQueryable();
        // 1. Tìm kiếm theo tên / giới thiệu
        if (!string.IsNullOrWhiteSpace(searchKeyword))
        {
            var keyword = searchKeyword.ToLower().Trim();
            query = query.Where(c => (c.Coach.FullName != null && c.Coach.FullName.ToLower().Contains(keyword)) ||
                                     (c.Bio != null && c.Bio.ToLower().Contains(keyword)));
        }
        // 2. Lọc theo năm kinh nghiệm tối thiểu
        if (minExperience.HasValue && minExperience.Value > 0)
        {
            query = query.Where(c => c.ExperienceYears >= minExperience.Value);
        }
        // 3. Sắp xếp (Sort)
        if (!string.IsNullOrWhiteSpace(sortBy))
        {
            query = sortBy.ToLower() switch
            {
                "exp_desc" => query.OrderByDescending(c => c.ExperienceYears),
                "exp_asc" => query.OrderBy(c => c.ExperienceYears),
                "name_asc" => query.OrderBy(c => c.Coach.FullName),
                "name_desc" => query.OrderByDescending(c => c.Coach.FullName),
                _ => query.OrderByDescending(c => c.ExperienceYears) // Mặc định
            };
        }
        else
        {
            // Mặc định nếu không truyền gì thì xếp kinh nghiệm từ cao xuống thấp
            query = query.OrderByDescending(c => c.ExperienceYears);
        }
        return await query.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<(List<CoachProfile> Items, int TotalCount)> GetPagedCoachApplicationsAsync(
        string? search = null,
        string? status = null,
        int? minExperience = null,
        int? maxExperience = null,
        string? sortBy = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .Include(c => c.Coach)
            .Include(c => c.ApprovedByNavigation)
            .Include(c => c.CoachCertificates)
            .Include(c => c.Locations)
            .AsNoTracking();

        // 1. Status Filter
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            var st = status.Trim().ToUpperInvariant();
            if (st == "PENDING")
            {
                query = query.Where(c =>
                    (c.ApprovalStatus == null || c.ApprovalStatus.ToUpper() == "PENDING") &&
                    (c.CoachCertificates.Any() || c.CertificateUrl != null) &&
                    (!c.CoachCertificates.Any() || c.CoachCertificates.Any(cert => cert.VerificationStatus == null || cert.VerificationStatus.ToUpper() != "REJECTED")));
            }
            else if (st == "REJECTED")
            {
                query = query.Where(c =>
                    (c.ApprovalStatus != null && c.ApprovalStatus.ToUpper() == "REJECTED") ||
                    ((c.ApprovalStatus == null || c.ApprovalStatus.ToUpper() == "PENDING") &&
                     ((!c.CoachCertificates.Any() && c.CertificateUrl == null) ||
                      (c.CoachCertificates.Any() && c.CoachCertificates.All(cert => cert.VerificationStatus != null && cert.VerificationStatus.ToUpper() == "REJECTED")))));
            }
            else
            {
                query = query.Where(c => c.ApprovalStatus != null && c.ApprovalStatus.ToUpper() == st);
            }
        }

        // 2. Search by applicant name, email, phone number, bio, or application ID (#APP-...)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLowerInvariant();
            var cleanCode = keyword.StartsWith("#app-") ? keyword[5..] : keyword.StartsWith("app-") ? keyword[4..] : keyword;
            query = query.Where(c =>
                (c.Coach.FullName != null && c.Coach.FullName.ToLower().Contains(keyword)) ||
                c.Coach.Email.ToLower().Contains(keyword) ||
                (c.Coach.PhoneNumber != null && c.Coach.PhoneNumber.ToLower().Contains(keyword)) ||
                (c.Bio != null && c.Bio.ToLower().Contains(keyword)) ||
                c.CoachId.ToString().ToLower().Contains(cleanCode));
        }

        // 3. Filter by experience
        if (minExperience.HasValue)
        {
            query = query.Where(c => c.ExperienceYears >= minExperience.Value);
        }
        if (maxExperience.HasValue)
        {
            query = query.Where(c => c.ExperienceYears <= maxExperience.Value);
        }

        // 4. Sorting
        query = (sortBy?.Trim().ToLowerInvariant()) switch
        {
            "submission_asc" => query.OrderBy(c => c.Coach.CreatedAt ?? c.UpdatedAt ?? DateTime.MinValue),
            "exp_desc" => query.OrderByDescending(c => c.ExperienceYears ?? 0),
            "exp_asc" => query.OrderBy(c => c.ExperienceYears ?? 0),
            "name_asc" => query.OrderBy(c => c.Coach.FullName ?? string.Empty),
            "name_desc" => query.OrderByDescending(c => c.Coach.FullName ?? string.Empty),
            _ => query.OrderByDescending(c => c.Coach.CreatedAt ?? c.UpdatedAt ?? DateTime.MinValue)
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

    public async Task<Dictionary<string, int>> GetCoachApplicationStatusCountsAsync(CancellationToken cancellationToken = default)
    {
        var allProfiles = await DbSet.AsNoTracking()
            .Include(c => c.CoachCertificates)
            .ToListAsync(cancellationToken);

        var total = allProfiles.Count;
        var pending = 0;
        var approved = 0;
        var rejected = 0;

        foreach (var p in allProfiles)
        {
            var certs = p.CoachCertificates?.ToList() ?? new List<CoachCertificate>();
            var hasNoCertificates = certs.Count == 0 && string.IsNullOrWhiteSpace(p.CertificateUrl);
            var allRejected = certs.Count > 0 && certs.All(cert => string.Equals(cert.VerificationStatus, "REJECTED", StringComparison.OrdinalIgnoreCase));
            var shouldAutoReject = hasNoCertificates || allRejected;

            var st = string.IsNullOrWhiteSpace(p.ApprovalStatus) ? "PENDING" : p.ApprovalStatus.Trim().ToUpperInvariant();
            if (shouldAutoReject && st == "PENDING")
            {
                st = "REJECTED";
            }

            if (st == "APPROVED") approved++;
            else if (st == "REJECTED") rejected++;
            else pending++;
        }

        return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "all", total },
            { "pending", pending },
            { "approved", approved },
            { "rejected", rejected }
        };
    }

    public Task<CoachProfile?> GetCoachApplicationDetailsAsync(Guid coachId, CancellationToken cancellationToken = default)
    {
        return DbSet
            .Include(p => p.Coach)
            .Include(p => p.ApprovedByNavigation)
            .Include(p => p.Locations)
            .Include(p => p.CoachCertificates)
            .Include(p => p.CoachEkycVerifications)
            .FirstOrDefaultAsync(p => p.CoachId == coachId, cancellationToken);
    }
}
