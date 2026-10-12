using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface ITrainingPlanRepository : IRepository<TrainingPlan>
{
    Task<TrainingPlan?> GetActiveByOrderDetailsIdAsync(Guid orderDetailsId, CancellationToken cancellationToken = default);
    Task<bool> HasActiveEnrollmentAsync(Guid buyerId, Guid packageId, CancellationToken cancellationToken = default);
    Task<TrainingPlan?> GetByIdWithExercisesAsync(Guid planId, bool asNoTracking = false, CancellationToken cancellationToken = default);
    Task<TrainingPlan?> GetByOrderDetailsIdWithExercisesAsync(Guid orderDetailsId, bool asNoTracking = true, CancellationToken cancellationToken = default);
    Task<OrderDetail?> GetOrderDetailWithOrderAndPackageAsync(Guid orderDetailsId, CancellationToken cancellationToken = default);
    Task AddVideoTutorialAsync(VideoTutorial videoTutorial, CancellationToken cancellationToken = default);
    Task<List<VideoTutorial>> GetCoachVideoTutorialsAsync(Guid coachId, CancellationToken cancellationToken = default);
    Task AddExerciseAsync(TrainingPlanExercise exercise, CancellationToken cancellationToken = default);
    void RemoveExercises(IEnumerable<TrainingPlanExercise> exercises);
}
