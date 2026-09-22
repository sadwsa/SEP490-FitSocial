using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface ICoachCertificateRepository : IRepository<CoachCertificate>
{
    Task<List<CoachCertificate>> ListByCoachIdAsync(Guid coachId, CancellationToken cancellationToken = default);
}
