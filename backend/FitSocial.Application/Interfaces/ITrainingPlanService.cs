using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.TrainingPlan;

namespace FitSocial.Application.Interfaces;

public interface ITrainingPlanService
{
    Task<TrainingPlanDetailDto> CreateTrainingPlanAsync(Guid coachId, CreateTrainingPlanDto dto, CancellationToken ct = default);
    Task<TrainingPlanDetailDto> UpdateTrainingPlanAsync(Guid coachId, Guid planId, UpdateTrainingPlanDto dto, CancellationToken ct = default);
    Task<TrainingPlanDetailDto?> GetTrainingPlanByOrderDetailsIdAsync(Guid coachId, Guid orderDetailsId, CancellationToken ct = default);
    Task<List<VideoTutorialDto>> GetCoachVideoTutorialsAsync(Guid coachId, CancellationToken ct = default);

    // Trainee Workout Hub & Daily Workout Marking
    Task<TraineeWorkoutPlanProgressDto> GetTraineeWorkoutPlanAsync(Guid traineeId, Guid orderDetailsId, CancellationToken ct = default);
    Task<TraineeWorkoutPlanProgressDto> GetTraineeWorkoutPlanByPackageAsync(Guid traineeId, Guid packageId, CancellationToken ct = default);
    Task<WorkoutCompletionResultDto> ToggleWorkoutCompletionAsync(Guid traineeId, ToggleWorkoutCompletionDto dto, CancellationToken ct = default);

    // Coach Progress Monitoring
    Task<CoachTraineeWorkoutProgressDto> GetCoachTraineeProgressAsync(Guid coachId, Guid orderDetailsId, CancellationToken ct = default);
}
