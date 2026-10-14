using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface ICoachCertificateRepository : IRepository<CoachCertificate>
{
    Task<List<CoachCertificate>> ListByCoachIdAsync(Guid coachId, CancellationToken cancellationToken = default);

    Task<(List<CoachCertificate> Items, int TotalCount)> GetPagedCertificatesAsync(
        string? search = null,
        string? status = null,
        string? sortBy = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    Task<Dictionary<string, int>> GetStatusCountsAsync(CancellationToken cancellationToken = default);

    Task<CoachCertificate?> GetCertificateWithDetailsAsync(Guid certificateId, CancellationToken cancellationToken = default);
}
