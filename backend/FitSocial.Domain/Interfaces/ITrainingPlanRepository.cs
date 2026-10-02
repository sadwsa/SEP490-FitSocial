using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface ITrainingPlanRepository : IRepository<TrainingPlan>
{
    Task<TrainingPlan?> GetActiveByOrderDetailsIdAsync(Guid orderDetailsId, CancellationToken cancellationToken = default);
    Task<bool> HasActiveEnrollmentAsync(Guid buyerId, Guid packageId, CancellationToken cancellationToken = default);
}
