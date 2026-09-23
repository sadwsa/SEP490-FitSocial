using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface ICoachEkycVerificationRepository : IRepository<CoachEkycVerification>
{
    Task<List<CoachEkycVerification>> ListByCoachIdAsync(Guid coachId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByIdCardNumberAsync(string idCardNumber, Guid? excludeCoachId = null, CancellationToken cancellationToken = default);
}
