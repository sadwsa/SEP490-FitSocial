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
}
