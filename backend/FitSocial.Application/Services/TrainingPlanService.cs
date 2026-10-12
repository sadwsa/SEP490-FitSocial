using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.TrainingPlan;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Application.Services;

public class TrainingPlanService : ITrainingPlanService
{
    private readonly ITrainingPlanRepository _trainingPlanRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TrainingPlanService(
        ITrainingPlanRepository trainingPlanRepository,
        IUnitOfWork unitOfWork)
    {
        _trainingPlanRepository = trainingPlanRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<TrainingPlanDetailDto> CreateTrainingPlanAsync(
        Guid coachId,
        CreateTrainingPlanDto dto,
        CancellationToken ct = default)
    {
        if (coachId == Guid.Empty)
        {
            throw new ForbiddenException("Invalid coach identity.");
        }

        if (dto == null)
        {
            throw new ValidationException("Request body cannot be null.");
        }

        if (dto.OrderDetailsId == Guid.Empty)
        {
            throw new ValidationException("OrderDetailsId is required.");
        }

        // Verify order detail exists and coach ownership
        var orderDetail = await _trainingPlanRepository.GetOrderDetailWithOrderAndPackageAsync(dto.OrderDetailsId, ct);
        if (orderDetail == null)
        {
            throw new NotFoundException("Order details not found.");
        }

        var isOwner = (orderDetail.Order != null && orderDetail.Order.CoachId == coachId) ||
                      (orderDetail.Package != null && orderDetail.Package.CoachId == coachId);

        if (!isOwner)
        {
            throw new ForbiddenException("You do not have permission to manage training plans for this order.");
        }

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var plan = new TrainingPlan
        {
            TrainingPlanId = Guid.NewGuid(),
            CoachId = coachId,
            OrderDetailsId = dto.OrderDetailsId,
            Title = dto.Title,
            Description = dto.Description,
            StartDate = dto.StartDate.HasValue ? DateTime.SpecifyKind(dto.StartDate.Value, DateTimeKind.Unspecified) : null,
            EndDate = dto.EndDate.HasValue ? DateTime.SpecifyKind(dto.EndDate.Value, DateTimeKind.Unspecified) : null,
            Status = !string.IsNullOrWhiteSpace(dto.Status) ? dto.Status : "ACTIVE",
            CreatedAt = now,
            UpdatedAt = now,
            PublishedAt = (string.Equals(dto.Status, "PUBLISHED", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(dto.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)) ? now : null
        };

        if (dto.Exercises != null && dto.Exercises.Count > 0)
        {
            short defaultSort = 1;
            foreach (var exDto in dto.Exercises)
            {
                Guid? videoTutorialId = exDto.VideoTutorialId;

                // Handle direct video upload or new video tutorial creation
                if (exDto.NewVideoTutorial != null && !string.IsNullOrWhiteSpace(exDto.NewVideoTutorial.VideoUrl))
                {
                    var newVideo = new VideoTutorial
                    {
                        VideoTutorialId = Guid.NewGuid(),
                        CoachId = coachId,
                        Title = !string.IsNullOrWhiteSpace(exDto.NewVideoTutorial.Title)
                            ? exDto.NewVideoTutorial.Title
                            : exDto.ExerciseName,
                        Description = exDto.NewVideoTutorial.Description,
                        VideoUrl = exDto.NewVideoTutorial.VideoUrl.Trim(),
                        CreatedAt = now
                    };

                    await _trainingPlanRepository.AddVideoTutorialAsync(newVideo, ct);
                    videoTutorialId = newVideo.VideoTutorialId;
                }

                var exercise = new TrainingPlanExercise
                {
                    TrainingPlanExerciseId = Guid.NewGuid(),
                    TrainingPlanId = plan.TrainingPlanId,
                    VideoTutorialId = videoTutorialId,
                    WeekNumber = exDto.WeekNumber,
                    DayNumber = exDto.DayNumber,
                    ExerciseName = exDto.ExerciseName,
                    Sets = exDto.Sets,
                    Reps = exDto.Reps,
                    DurationMinutes = exDto.DurationMinutes,
                    RestSeconds = exDto.RestSeconds,
                    Notes = exDto.Notes,
                    SortOrder = exDto.SortOrder ?? defaultSort++
                };

                plan.TrainingPlanExercises.Add(exercise);
            }
        }

        await _trainingPlanRepository.AddAsync(plan, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // Retrieve full eager-loaded details to return
        var created = await _trainingPlanRepository.GetByIdWithExercisesAsync(plan.TrainingPlanId, asNoTracking: true, ct);
        return MapToDetailDto(created ?? plan);
    }

    public async Task<TrainingPlanDetailDto> UpdateTrainingPlanAsync(
        Guid coachId,
        Guid planId,
        UpdateTrainingPlanDto dto,
        CancellationToken ct = default)
    {
        if (coachId == Guid.Empty)
        {
            throw new ForbiddenException("Invalid coach identity.");
        }

        if (dto == null)
        {
            throw new ValidationException("Request body cannot be null.");
        }

        var existingPlan = await _trainingPlanRepository.GetByIdWithExercisesAsync(planId, asNoTracking: false, ct);
        if (existingPlan == null)
        {
            throw new NotFoundException("Training plan not found.");
        }

        if (existingPlan.CoachId != coachId)
        {
            throw new ForbiddenException("You do not have permission to update this training plan.");
        }

        if (existingPlan.OrderDetails != null)
        {
            var isOrderOwner = (existingPlan.OrderDetails.Order != null && existingPlan.OrderDetails.Order.CoachId == coachId) ||
                               (existingPlan.OrderDetails.Package != null && existingPlan.OrderDetails.Package.CoachId == coachId);

            if (!isOrderOwner)
            {
                throw new ForbiddenException("You do not have permission to update training plan for this order.");
            }
        }

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        if (dto.Title != null) existingPlan.Title = dto.Title;
        if (dto.Description != null) existingPlan.Description = dto.Description;
        if (dto.StartDate.HasValue) existingPlan.StartDate = DateTime.SpecifyKind(dto.StartDate.Value, DateTimeKind.Unspecified);
        if (dto.EndDate.HasValue) existingPlan.EndDate = DateTime.SpecifyKind(dto.EndDate.Value, DateTimeKind.Unspecified);
        if (dto.Status != null)
        {
            existingPlan.Status = dto.Status;
            if (string.Equals(dto.Status, "PUBLISHED", StringComparison.OrdinalIgnoreCase) && existingPlan.PublishedAt == null)
            {
                existingPlan.PublishedAt = now;
            }
        }
        existingPlan.UpdatedAt = now;

        if (dto.Exercises != null)
        {
            // 1. Identify exercises to remove
            var incomingIds = dto.Exercises
                .Where(e => e.TrainingPlanExerciseId.HasValue && e.TrainingPlanExerciseId.Value != Guid.Empty)
                .Select(e => e.TrainingPlanExerciseId!.Value)
                .ToHashSet();

            var exercisesToRemove = existingPlan.TrainingPlanExercises
                .Where(e => !incomingIds.Contains(e.TrainingPlanExerciseId))
                .ToList();

            if (exercisesToRemove.Count > 0)
            {
                _trainingPlanRepository.RemoveExercises(exercisesToRemove);
            }

            // 2. Add or update incoming exercises
            short defaultSort = 1;
            foreach (var exDto in dto.Exercises)
            {
                Guid? videoTutorialId = exDto.VideoTutorialId;

                // Create new video tutorial if requested
                if (exDto.NewVideoTutorial != null && !string.IsNullOrWhiteSpace(exDto.NewVideoTutorial.VideoUrl))
                {
                    var newVideo = new VideoTutorial
                    {
                        VideoTutorialId = Guid.NewGuid(),
                        CoachId = coachId,
                        Title = !string.IsNullOrWhiteSpace(exDto.NewVideoTutorial.Title)
                            ? exDto.NewVideoTutorial.Title
                            : exDto.ExerciseName,
                        Description = exDto.NewVideoTutorial.Description,
                        VideoUrl = exDto.NewVideoTutorial.VideoUrl.Trim(),
                        CreatedAt = now
                    };

                    await _trainingPlanRepository.AddVideoTutorialAsync(newVideo, ct);
                    videoTutorialId = newVideo.VideoTutorialId;
                }

                if (exDto.TrainingPlanExerciseId.HasValue && exDto.TrainingPlanExerciseId.Value != Guid.Empty)
                {
                    var existingExercise = existingPlan.TrainingPlanExercises
                        .FirstOrDefault(e => e.TrainingPlanExerciseId == exDto.TrainingPlanExerciseId.Value);

                    if (existingExercise != null)
                    {
                        existingExercise.ExerciseName = exDto.ExerciseName;
                        existingExercise.WeekNumber = exDto.WeekNumber;
                        existingExercise.DayNumber = exDto.DayNumber;
                        existingExercise.Sets = exDto.Sets;
                        existingExercise.Reps = exDto.Reps;
                        existingExercise.DurationMinutes = exDto.DurationMinutes;
                        existingExercise.RestSeconds = exDto.RestSeconds;
                        existingExercise.Notes = exDto.Notes;
                        existingExercise.SortOrder = exDto.SortOrder ?? defaultSort++;
                        if (videoTutorialId.HasValue)
                        {
                            existingExercise.VideoTutorialId = videoTutorialId;
                        }
                        continue;
                    }
                }

                // New exercise - explicitly added to repository so EF Core marks EntityState.Added
                var newExercise = new TrainingPlanExercise
                {
                    TrainingPlanExerciseId = Guid.NewGuid(),
                    TrainingPlanId = existingPlan.TrainingPlanId,
                    VideoTutorialId = videoTutorialId,
                    WeekNumber = exDto.WeekNumber,
                    DayNumber = exDto.DayNumber,
                    ExerciseName = exDto.ExerciseName,
                    Sets = exDto.Sets,
                    Reps = exDto.Reps,
                    DurationMinutes = exDto.DurationMinutes,
                    RestSeconds = exDto.RestSeconds,
                    Notes = exDto.Notes,
                    SortOrder = exDto.SortOrder ?? defaultSort++
                };

                await _trainingPlanRepository.AddExerciseAsync(newExercise, ct);
                existingPlan.TrainingPlanExercises.Add(newExercise);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);

        var updated = await _trainingPlanRepository.GetByIdWithExercisesAsync(planId, asNoTracking: true, ct);
        return MapToDetailDto(updated ?? existingPlan);
    }

    public async Task<TrainingPlanDetailDto?> GetTrainingPlanByOrderDetailsIdAsync(
        Guid coachId,
        Guid orderDetailsId,
        CancellationToken ct = default)
    {
        if (coachId == Guid.Empty)
        {
            throw new ForbiddenException("Invalid coach identity.");
        }

        var orderDetail = await _trainingPlanRepository.GetOrderDetailWithOrderAndPackageAsync(orderDetailsId, ct);
        if (orderDetail == null)
        {
            throw new NotFoundException("Order details not found.");
        }

        var isOwner = (orderDetail.Order != null && orderDetail.Order.CoachId == coachId) ||
                      (orderDetail.Package != null && orderDetail.Package.CoachId == coachId);

        if (!isOwner)
        {
            throw new ForbiddenException("You do not have permission to view training plan for this order.");
        }

        var plan = await _trainingPlanRepository.GetByOrderDetailsIdWithExercisesAsync(orderDetailsId, asNoTracking: true, ct);
        if (plan == null)
        {
            return null;
        }

        return MapToDetailDto(plan);
    }

    public async Task<List<VideoTutorialDto>> GetCoachVideoTutorialsAsync(
        Guid coachId,
        CancellationToken ct = default)
    {
        if (coachId == Guid.Empty)
        {
            throw new ForbiddenException("Invalid coach identity.");
        }

        var videos = await _trainingPlanRepository.GetCoachVideoTutorialsAsync(coachId, ct);
        return videos.Select(MapVideoToDto).ToList();
    }

    private static TrainingPlanDetailDto MapToDetailDto(TrainingPlan entity)
    {
        return new TrainingPlanDetailDto
        {
            TrainingPlanId = entity.TrainingPlanId,
            CoachId = entity.CoachId,
            OrderDetailsId = entity.OrderDetailsId,
            Title = entity.Title,
            Description = entity.Description,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            PublishedAt = entity.PublishedAt,
            Exercises = entity.TrainingPlanExercises?
                .OrderBy(e => e.WeekNumber ?? 1)
                .ThenBy(e => e.DayNumber ?? 1)
                .ThenBy(e => e.SortOrder ?? 1)
                .Select(e => new TrainingPlanExerciseDetailDto
                {
                    TrainingPlanExerciseId = e.TrainingPlanExerciseId,
                    TrainingPlanId = e.TrainingPlanId,
                    VideoTutorialId = e.VideoTutorialId,
                    WeekNumber = e.WeekNumber,
                    DayNumber = e.DayNumber,
                    ExerciseName = e.ExerciseName,
                    Sets = e.Sets,
                    Reps = e.Reps,
                    DurationMinutes = e.DurationMinutes,
                    RestSeconds = e.RestSeconds,
                    Notes = e.Notes,
                    SortOrder = e.SortOrder,
                    VideoTutorial = e.VideoTutorial != null ? MapVideoToDto(e.VideoTutorial) : null
                }).ToList() ?? new List<TrainingPlanExerciseDetailDto>()
        };
    }

    private static VideoTutorialDto MapVideoToDto(VideoTutorial video)
    {
        return new VideoTutorialDto
        {
            VideoTutorialId = video.VideoTutorialId,
            CoachId = video.CoachId,
            Title = video.Title,
            Description = video.Description,
            VideoUrl = video.VideoUrl,
            CreatedAt = video.CreatedAt
        };
    }
}
