using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<bool> ExistsByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);
    Task<bool> ExistsByPhoneAsync(string phoneNumber, Guid? excludingUserId = null, CancellationToken cancellationToken = default);
    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);
    Task<User?> FindByGoogleSubAsync(string googleSub, CancellationToken cancellationToken = default);
}
