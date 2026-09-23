using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface IUserAgreementRepository : IRepository<UserAgreement>
{
    Task<UserAgreement?> FindByUserAndTermAsync(Guid userId, Guid termId, CancellationToken cancellationToken = default);
}
