using FitSocial.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Domain.Interfaces;

public interface IReviewRepository : IRepository<Review>
{
    Task<List<Review>> GetReviewsByPackageIdAsync(Guid packageId, CancellationToken cancellationToken = default);
    Task<Review?> GetReviewByPackageAndTraineeAsync(Guid packageId, Guid traineeId, CancellationToken cancellationToken = default);
}
