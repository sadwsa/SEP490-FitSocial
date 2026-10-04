using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class TrainingPlanRepository : Repository<TrainingPlan>, ITrainingPlanRepository
{
    public TrainingPlanRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public Task<TrainingPlan?> GetActiveByOrderDetailsIdAsync(Guid orderDetailsId, CancellationToken cancellationToken = default)
    {
        return DbSet.FirstOrDefaultAsync(
            t => t.OrderDetailsId == orderDetailsId && t.Status == "ACTIVE",
            cancellationToken);
    }

    public Task<bool> HasActiveEnrollmentAsync(Guid buyerId, Guid packageId, CancellationToken cancellationToken = default)
    {
        // Enrollment = TrainingPlan ACTIVE linked to an OrderDetail of this buyer+package
        // via Order -> OrderDetails -> TrainingPlans
        return DbContext.TrainingPlans
            .Include(t => t.OrderDetails)
                .ThenInclude(od => od!.Order)
            .AnyAsync(t =>
                t.Status == "ACTIVE"
                && t.OrderDetails != null
                && t.OrderDetails.PackageId == packageId
                && t.OrderDetails.Order != null
                && t.OrderDetails.Order.BuyerId == buyerId,
                cancellationToken);
    }
}
