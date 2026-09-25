using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface ITermsAndPolicyRepository : IRepository<TermsAndPolicy>
{
    Task<TermsAndPolicy?> GetLatestAsync(CancellationToken cancellationToken = default);
}
