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

    public async Task<TraineeWorkoutPlanProgressDto> GetTraineeWorkoutPlanAsync(
        Guid traineeId,
        Guid orderDetailsId,
        CancellationToken ct = default)
    {
        if (traineeId == Guid.Empty)
        {
            throw new ForbiddenException("Invalid trainee identity.");
        }

        var plan = await _trainingPlanRepository.GetTraineePlanWithLogsAsync(orderDetailsId, traineeId, ct);
        if (plan == null)
        {
            var od = await _trainingPlanRepository.GetOrderDetailWithOrderAndPackageAsync(orderDetailsId, ct);
            if (od == null || od.Order == null || od.Order.BuyerId != traineeId)
            {
                throw new NotFoundException("Order detail not found or does not belong to you.");
            }

            return new TraineeWorkoutPlanProgressDto
            {
                TrainingPlanId = Guid.Empty,
                OrderDetailsId = orderDetailsId,
                PackageId = od.PackageId ?? Guid.Empty,
                PackageTitle = od.PackageTitle ?? od.Package?.Title ?? "Training Package",
                PlanTitle = "Training Plan Not Ready",
                PlanDescription = "Your coach is currently creating your personalized training program. Please check back soon!",
                PlanStatus = "PENDING_COACH",
                CoachId = od.Order?.CoachId ?? od.Package?.CoachId ?? Guid.Empty,
                CoachName = od.CoachName ?? od.Package?.Coach?.Coach?.FullName ?? "Your Coach"
            };
        }

        await BackfillIncompleteLogsAsync(plan, traineeId, ct);
        return MapToTraineeProgressDto(plan, traineeId);
    }

    public async Task<TraineeWorkoutPlanProgressDto> GetTraineeWorkoutPlanByPackageAsync(
        Guid traineeId,
        Guid packageId,
        CancellationToken ct = default)
    {
        if (traineeId == Guid.Empty)
        {
            throw new ForbiddenException("Invalid trainee identity.");
        }

        var plan = await _trainingPlanRepository.GetTraineePlanByPackageIdWithLogsAsync(packageId, traineeId, ct);
        if (plan == null)
        {
            throw new NotFoundException("No active training plan found for this purchased package.");
        }

        await BackfillIncompleteLogsAsync(plan, traineeId, ct);
        return MapToTraineeProgressDto(plan, traineeId);
    }

    /// <summary>
    /// Records an explicit INCOMPLETE log for every scheduled exercise whose day has already
    /// passed and that has no log yet. Completed exercises are left untouched.
    /// Idempotent: running it again only fills the gaps that are still missing.
    /// </summary>
    private async Task BackfillIncompleteLogsAsync(TrainingPlan plan, Guid traineeId, CancellationToken ct)
    {
        if (!plan.StartDate.HasValue)
        {
            return;
        }

        var todayUtc = DateTime.UtcNow.Date;
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var created = false;

        foreach (var exercise in plan.TrainingPlanExercises ?? new List<TrainingPlanExercise>())
        {
            var scheduledDate = ResolveScheduledDate(plan.StartDate, plan.EndDate, exercise.WeekNumber ?? 1, exercise.DayNumber ?? 1);
            if (!scheduledDate.HasValue || scheduledDate.Value.Date >= todayUtc)
            {
                // Outside the plan range, not scheduled yet, or scheduled for today/future -> skip.
                continue;
            }

            var alreadyLogged = exercise.WorkoutCompletionLogs?
                .Any(l => l.TraineeId == traineeId) == true;
            if (alreadyLogged)
            {
                continue;
            }

            await _trainingPlanRepository.AddWorkoutLogAsync(new WorkoutCompletionLog
            {
                WorkoutCompletionLogId = Guid.NewGuid(),
                TraineeId = traineeId,
                TrainingPlanExerciseId = exercise.TrainingPlanExerciseId,
                LogDate = scheduledDate.Value,
                Status = "INCOMPLETE",
                CreatedAt = now
            }, ct);

            created = true;
        }

        if (created)
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    public async Task<WorkoutCompletionResultDto> ToggleWorkoutCompletionAsync(
        Guid traineeId,
        ToggleWorkoutCompletionDto dto,
        CancellationToken ct = default)
    {
        if (traineeId == Guid.Empty)
        {
            throw new ForbiddenException("Invalid trainee identity.");
        }

        if (dto == null || dto.TrainingPlanExerciseId == Guid.Empty)
        {
            throw new ValidationException("TrainingPlanExerciseId is required.");
        }

        var exercise = await _trainingPlanRepository.GetExerciseByIdAsync(dto.TrainingPlanExerciseId, ct);
        if (exercise == null)
        {
            throw new NotFoundException("Exercise not found.");
        }

        var orderBuyerId = exercise.TrainingPlan?.OrderDetails?.Order?.BuyerId;
        if (orderBuyerId.HasValue && orderBuyerId.Value != traineeId)
        {
            throw new ForbiddenException("You are not authorized to mark this exercise.");
        }

        var existingLog = await _trainingPlanRepository.GetWorkoutLogAsync(dto.TrainingPlanExerciseId, traineeId, ct);
        bool isNowCompleted;
        Guid? logId = null;
        DateTime? completedAt = null;
        string? savedNotes = dto.Notes;

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        if (existingLog != null)
        {
            if (string.Equals(dto.Status, "INCOMPLETE", StringComparison.OrdinalIgnoreCase))
            {
                // Persist "not completed" instead of deleting the row so adherence can be measured later.
                existingLog.Status = "INCOMPLETE";
                if (!string.IsNullOrWhiteSpace(dto.Notes)) existingLog.Notes = dto.Notes;
                if (dto.LogDate.HasValue)
                {
                    existingLog.LogDate = DateTime.SpecifyKind(dto.LogDate.Value, DateTimeKind.Unspecified);
                }
                isNowCompleted = false;
                logId = existingLog.WorkoutCompletionLogId;
                savedNotes = existingLog.Notes;
            }
            else if (string.Equals(existingLog.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(dto.Notes) && string.IsNullOrWhiteSpace(dto.Status))
            {
                // Toggle OFF -> Delete log
                _trainingPlanRepository.RemoveWorkoutLog(existingLog);
                isNowCompleted = false;
            }
            else
            {
                // Update notes or status
                if (!string.IsNullOrWhiteSpace(dto.Notes)) existingLog.Notes = dto.Notes;
                if (!string.IsNullOrWhiteSpace(dto.Status)) existingLog.Status = dto.Status;
                isNowCompleted = string.Equals(existingLog.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase);
                logId = existingLog.WorkoutCompletionLogId;
                completedAt = existingLog.CreatedAt;
                savedNotes = existingLog.Notes;
            }
        }
        else
        {
            // Toggle ON -> Add new log
            var newLog = new WorkoutCompletionLog
            {
                WorkoutCompletionLogId = Guid.NewGuid(),
                TraineeId = traineeId,
                TrainingPlanExerciseId = dto.TrainingPlanExerciseId,
                LogDate = dto.LogDate.HasValue ? DateTime.SpecifyKind(dto.LogDate.Value, DateTimeKind.Unspecified) : now,
                Status = !string.IsNullOrWhiteSpace(dto.Status) ? dto.Status : "COMPLETED",
                Notes = dto.Notes,
                CreatedAt = now
            };

            await _trainingPlanRepository.AddWorkoutLogAsync(newLog, ct);
            isNowCompleted = true;
            logId = newLog.WorkoutCompletionLogId;
            completedAt = newLog.CreatedAt;
            savedNotes = newLog.Notes;
        }

        await _unitOfWork.SaveChangesAsync(ct);

        // Fetch refreshed progress for the parent plan
        var plan = await _trainingPlanRepository.GetTraineePlanWithLogsAsync(
            exercise.TrainingPlan?.OrderDetailsId ?? Guid.Empty, traineeId, ct);

        int totalCount = plan?.TrainingPlanExercises?.Count ?? 0;
        int completedCount = plan?.TrainingPlanExercises?
            .Count(e => e.WorkoutCompletionLogs?.Any(l => l.TraineeId == traineeId && l.Status == "COMPLETED") == true) ?? 0;
        double percentage = totalCount > 0 ? Math.Round((double)completedCount / totalCount * 100, 1) : 0;

        return new WorkoutCompletionResultDto
        {
            TrainingPlanExerciseId = dto.TrainingPlanExerciseId,
            IsCompleted = isNowCompleted,
            WorkoutCompletionLogId = logId,
            CompletedAt = completedAt,
            Status = isNowCompleted ? "COMPLETED" : "INCOMPLETE",
            Notes = savedNotes,
            TotalExercisesCount = totalCount,
            CompletedExercisesCount = completedCount,
            ProgressPercentage = percentage
        };
    }

    public async Task<CoachTraineeWorkoutProgressDto> GetCoachTraineeProgressAsync(
        Guid coachId,
        Guid orderDetailsId,
        CancellationToken ct = default)
    {
        if (coachId == Guid.Empty)
        {
            throw new ForbiddenException("Invalid coach identity.");
        }

        var plan = await _trainingPlanRepository.GetCoachTraineePlanWithLogsAsync(orderDetailsId, coachId, ct);
        if (plan == null)
        {
            var od = await _trainingPlanRepository.GetOrderDetailWithOrderAndPackageAsync(orderDetailsId, ct);
            if (od == null || (od.Order?.CoachId != coachId && od.Package?.CoachId != coachId))
            {
                throw new NotFoundException("Order detail not found or does not belong to you.");
            }

            return new CoachTraineeWorkoutProgressDto
            {
                OrderDetailsId = orderDetailsId,
                TraineeId = od.Order?.BuyerId ?? Guid.Empty,
                TraineeName = od.Order?.Buyer?.FullName ?? "Trainee",
                TraineeEmail = od.Order?.Buyer?.Email ?? string.Empty,
                PackageId = od.PackageId ?? Guid.Empty,
                PackageTitle = od.PackageTitle ?? od.Package?.Title ?? "Training Package",
                PlanTitle = "No Plan Created",
                PlanStatus = "DRAFT"
            };
        }

        var buyer = plan.OrderDetails?.Order?.Buyer;
        var weeks = BuildWeeksBreakdown(plan.TrainingPlanExercises, buyer?.UserId, plan.StartDate, plan.EndDate);

        var allLogs = plan.TrainingPlanExercises
            .SelectMany(e => e.WorkoutCompletionLogs.Select(l => new { Exercise = e, Log = l }))
            .OrderByDescending(x => x.Log.CreatedAt)
            .Take(30)
            .Select(x => new CoachWorkoutActivityLogDto
            {
                LogId = x.Log.WorkoutCompletionLogId,
                ExerciseName = x.Exercise.ExerciseName ?? "Exercise",
                WeekNumber = x.Exercise.WeekNumber,
                DayNumber = x.Exercise.DayNumber,
                CompletedAt = x.Log.CreatedAt ?? DateTime.UtcNow,
                Status = x.Log.Status,
                Notes = x.Log.Notes
            })
            .ToList();

        int totalCount = weeks.Sum(w => w.TotalExercises);
        int completedCount = weeks.Sum(w => w.CompletedExercises);

        return new CoachTraineeWorkoutProgressDto
        {
            OrderDetailsId = orderDetailsId,
            TraineeId = buyer?.UserId ?? Guid.Empty,
            TraineeName = buyer?.FullName ?? "Trainee",
            TraineeEmail = buyer?.Email ?? string.Empty,
            TraineeAvatarUrl = buyer?.AvatarUrl,
            TraineePhone = buyer?.PhoneNumber,
            PackageId = plan.OrderDetails?.PackageId ?? Guid.Empty,
            PackageTitle = plan.OrderDetails?.PackageTitle ?? plan.OrderDetails?.Package?.Title ?? "Training Package",
            TrainingPlanId = plan.TrainingPlanId,
            PlanTitle = plan.Title,
            PlanStatus = plan.Status,
            StartDate = plan.StartDate,
            EndDate = plan.EndDate,
            TotalExercisesCount = totalCount,
            CompletedExercisesCount = completedCount,
            Weeks = weeks,
            RecentLogs = allLogs
        };
    }

    private static TraineeWorkoutPlanProgressDto MapToTraineeProgressDto(TrainingPlan plan, Guid traineeId)
    {
        var exercises = plan.TrainingPlanExercises ?? new List<TrainingPlanExercise>();
        var weeks = BuildWeeksBreakdown(exercises, traineeId, plan.StartDate, plan.EndDate);

        int totalCount = weeks.Sum(w => w.TotalExercises);
        int completedCount = weeks.Sum(w => w.CompletedExercises);

        return new TraineeWorkoutPlanProgressDto
        {
            TrainingPlanId = plan.TrainingPlanId,
            OrderDetailsId = plan.OrderDetailsId ?? Guid.Empty,
            PackageId = plan.OrderDetails?.PackageId ?? Guid.Empty,
            PackageTitle = plan.OrderDetails?.PackageTitle ?? plan.OrderDetails?.Package?.Title ?? "Training Package",
            PackageThumbnailUrl = plan.OrderDetails?.Package?.Media?.OrderBy(m => m.SortOrder).FirstOrDefault()?.MediaUrl,
            PlanTitle = plan.Title ?? "Training Plan",
            PlanDescription = plan.Description,
            PlanStatus = plan.Status,
            StartDate = plan.StartDate,
            EndDate = plan.EndDate,
            CoachId = plan.CoachId,
            CoachName = plan.Coach?.Coach?.FullName ?? "Coach",
            CoachAvatarUrl = plan.Coach?.Coach?.AvatarUrl,
            TotalExercisesCount = totalCount,
            CompletedExercisesCount = completedCount,
            Weeks = weeks
        };
    }

    private static List<TraineeWorkoutWeekDto> BuildWeeksBreakdown(
        IEnumerable<TrainingPlanExercise> exercises,
        Guid? filterTraineeId,
        DateTime? planStartDate = null,
        DateTime? planEndDate = null)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var weeks = new List<TraineeWorkoutWeekDto>();
        var groupedByWeek = exercises
            .GroupBy(e => e.WeekNumber ?? 1)
            .OrderBy(g => g.Key);

        foreach (var weekGroup in groupedByWeek)
        {
            var weekDto = new TraineeWorkoutWeekDto
            {
                WeekNumber = weekGroup.Key,
                Title = $"Week {weekGroup.Key}"
            };

            var groupedByDay = weekGroup
                .GroupBy(e => e.DayNumber ?? 1)
                .OrderBy(g => g.Key);

            foreach (var dayGroup in groupedByDay)
            {
                var scheduledDate = ResolveScheduledDate(planStartDate, planEndDate, weekGroup.Key, dayGroup.Key);

                // A plan without a StartDate is not on a calendar, so every day stays markable.
                var hasSchedule = planStartDate.HasValue;

                var dayDto = new TraineeWorkoutDayDto
                {
                    DayNumber = dayGroup.Key,
                    Title = $"Day {dayGroup.Key}",
                    ScheduledDate = scheduledDate,
                    IsToday = !hasSchedule || (scheduledDate.HasValue && scheduledDate.Value.Date == todayUtc)
                };

                foreach (var ex in dayGroup.OrderBy(e => e.SortOrder ?? 1))
                {
                    // Scheduled outside [StartDate, EndDate] -> hide it from the trainee entirely.
                    if (hasSchedule && !scheduledDate.HasValue)
                    {
                        break;
                    }

                    // Prefer a COMPLETED log; fall back to the most recent log (e.g. INCOMPLETE).
                    WorkoutCompletionLog? relevantLog = null;
                    if (ex.WorkoutCompletionLogs != null)
                    {
                        var mine = ex.WorkoutCompletionLogs
                            .Where(l => !filterTraineeId.HasValue || l.TraineeId == filterTraineeId.Value)
                            .ToList();

                        relevantLog = mine.FirstOrDefault(l => string.Equals(l.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
                                      ?? mine.OrderByDescending(l => l.CreatedAt).FirstOrDefault();
                    }

                    var isCompleted = string.Equals(relevantLog?.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase);

                    dayDto.Exercises.Add(new TraineeWorkoutExerciseDto
                    {
                        TrainingPlanExerciseId = ex.TrainingPlanExerciseId,
                        TrainingPlanId = ex.TrainingPlanId,
                        WeekNumber = ex.WeekNumber,
                        DayNumber = ex.DayNumber,
                        ExerciseName = ex.ExerciseName ?? "Exercise",
                        Sets = ex.Sets,
                        Reps = ex.Reps,
                        DurationMinutes = ex.DurationMinutes,
                        RestSeconds = ex.RestSeconds,
                        Notes = ex.Notes,
                        SortOrder = ex.SortOrder,
                        VideoTutorial = ex.VideoTutorial != null ? MapVideoToDto(ex.VideoTutorial) : null,
                        IsCompleted = isCompleted,
                        WorkoutCompletionLogId = relevantLog?.WorkoutCompletionLogId,
                        CompletedAt = relevantLog?.CreatedAt,
                        TraineeNotes = relevantLog?.Notes,
                        Status = relevantLog?.Status,
                        ScheduledDate = scheduledDate
                    });
                }

                dayDto.TotalExercises = dayDto.Exercises.Count;
                dayDto.CompletedExercises = dayDto.Exercises.Count(e => e.IsCompleted);

                // Every exercise of this day fell outside the plan range: drop the day too.
                if (dayDto.TotalExercises == 0)
                {
                    continue;
                }

                weekDto.Days.Add(dayDto);
            }

            if (weekDto.Days.Count == 0)
            {
                continue;
            }

            weekDto.TotalExercises = weekDto.Days.Sum(d => d.TotalExercises);
            weekDto.CompletedExercises = weekDto.Days.Sum(d => d.CompletedExercises);
            weeks.Add(weekDto);
        }

        return weeks;
    }

    /// <summary>
    /// Maps a (WeekNumber, DayNumber) pair onto a real calendar date using the plan StartDate.
    /// Weeks are treated as 7-day blocks, so Week 3 Day 3 = StartDate + 17 days.
    /// Returns null when the plan has no StartDate or the date falls outside [StartDate, EndDate].
    /// </summary>
    private static DateTime? ResolveScheduledDate(DateTime? planStartDate, DateTime? planEndDate, short weekNumber, short dayNumber)
    {
        if (!planStartDate.HasValue)
        {
            return null;
        }

        var start = planStartDate.Value.Date;
        var offset = ((weekNumber - 1) * 7) + (dayNumber - 1);
        var date = start.AddDays(offset);

        if (date < start)
        {
            return null;
        }

        if (planEndDate.HasValue && date > planEndDate.Value.Date)
        {
            return null;
        }

        return date;
    }
}
