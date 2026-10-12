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
}
