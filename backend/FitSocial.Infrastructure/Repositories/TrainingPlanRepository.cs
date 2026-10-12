using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

    public async Task<TrainingPlan?> GetByIdWithExercisesAsync(Guid planId, bool asNoTracking = false, CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsQueryable();
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query
            .Include(p => p.OrderDetails)
                .ThenInclude(od => od!.Order)
            .Include(p => p.OrderDetails)
                .ThenInclude(od => od!.Package)
            .Include(p => p.TrainingPlanExercises)
                .ThenInclude(e => e.VideoTutorial)
            .FirstOrDefaultAsync(p => p.TrainingPlanId == planId, cancellationToken);
    }

    public async Task<TrainingPlan?> GetByOrderDetailsIdWithExercisesAsync(Guid orderDetailsId, bool asNoTracking = true, CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsQueryable();
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query
            .Include(p => p.OrderDetails)
                .ThenInclude(od => od!.Order)
            .Include(p => p.OrderDetails)
                .ThenInclude(od => od!.Package)
            .Include(p => p.TrainingPlanExercises)
                .ThenInclude(e => e.VideoTutorial)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(p => p.OrderDetailsId == orderDetailsId, cancellationToken);
    }

    public async Task<OrderDetail?> GetOrderDetailWithOrderAndPackageAsync(Guid orderDetailsId, CancellationToken cancellationToken = default)
    {
        return await DbContext.OrderDetails
            .Include(od => od.Order)
            .Include(od => od.Package)
            .AsNoTracking()
            .FirstOrDefaultAsync(od => od.OrderDetailsId == orderDetailsId, cancellationToken);
    }

    public async Task AddVideoTutorialAsync(VideoTutorial videoTutorial, CancellationToken cancellationToken = default)
    {
        await DbContext.VideoTutorials.AddAsync(videoTutorial, cancellationToken);
    }

    public async Task<List<VideoTutorial>> GetCoachVideoTutorialsAsync(Guid coachId, CancellationToken cancellationToken = default)
    {
        return await DbContext.VideoTutorials
            .Where(v => v.CoachId == coachId)
            .OrderByDescending(v => v.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task AddExerciseAsync(TrainingPlanExercise exercise, CancellationToken cancellationToken = default)
    {
        await DbContext.TrainingPlanExercises.AddAsync(exercise, cancellationToken);
    }

    public void RemoveExercises(IEnumerable<TrainingPlanExercise> exercises)
    {
        DbContext.TrainingPlanExercises.RemoveRange(exercises);
    }
}
