using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface IOtpLogRepository : IRepository<Otplog>
{
    Task<List<Otplog>> ListUnverifiedAsync(string normalizedEmail, string purpose, CancellationToken cancellationToken = default);
    Task<Otplog?> FindLatestUnverifiedAsync(string normalizedEmail, string purpose, CancellationToken cancellationToken = default);
}
