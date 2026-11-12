using FitSocial.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Domain.Interfaces;

public interface IReviewMediaRepository : IRepository<ReviewMedia>
{
    Task<List<ReviewMedia>> GetMediaByReviewIdAsync(Guid reviewId, CancellationToken cancellationToken = default);
    Task<List<ReviewMedia>> GetMediaByPackageIdAsync(Guid packageId, CancellationToken cancellationToken = default);
}
