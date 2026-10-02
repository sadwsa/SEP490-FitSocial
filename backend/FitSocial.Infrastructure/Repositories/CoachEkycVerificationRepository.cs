using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Infrastructure.Repositories;

public class CoachEkycVerificationRepository : Repository<CoachEkycVerification>, ICoachEkycVerificationRepository
{
    private readonly IEncryptionService _encryptionService;

    public CoachEkycVerificationRepository(FitSocialDbContext dbContext, IEncryptionService encryptionService) : base(dbContext)
    {
        _encryptionService = encryptionService;
    }

    public Task<List<CoachEkycVerification>> ListByCoachIdAsync(Guid coachId, CancellationToken cancellationToken = default)
    {
        return DbSet.Where(e => e.CoachId == coachId).ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsByIdCardNumberAsync(string idCardNumber, Guid? excludeCoachId = null, CancellationToken cancellationToken = default)
    {
        var encryptedBytes = _encryptionService.Encrypt(idCardNumber, deterministic: true);
        var q = DbSet.Where(e => e.EncryptedIdCardNumber == encryptedBytes);
        if (excludeCoachId.HasValue) q = q.Where(e => e.CoachId != excludeCoachId.Value);
        return q.AnyAsync(cancellationToken);
    }
}
