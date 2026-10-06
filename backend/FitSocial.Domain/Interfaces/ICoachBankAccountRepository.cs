using FitSocial.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Domain.Interfaces;

public interface ICoachBankAccountRepository : IRepository<CoachBankAccount>
{
    Task<List<CoachBankAccount>> ListByCoachIdAsync(Guid coachId, CancellationToken cancellationToken = default);
    Task<CoachBankAccount?> GetDefaultByCoachIdAsync(Guid coachId, CancellationToken cancellationToken = default);

    /// <summary>
    /// UC-36: all coach payment accounts with coach user info for the admin list.
    /// </summary>
    Task<List<CoachBankAccount>> ListAllWithCoachAsync(string? searchTerm, string? bankCode, bool? defaultOnly, CancellationToken cancellationToken = default);
}
