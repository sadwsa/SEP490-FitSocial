using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Domain.Models.Finance;

namespace FitSocial.Domain.Interfaces;

/// <summary>
/// Read-only access to the unified "money leaving the platform" ledger
/// (coach payout lines + trainee refund requests).
/// </summary>
public interface IMoneyMovementRepository
{
    Task<List<MoneyMovementRecordModel>> ListAllAsync(CancellationToken ct = default);
}