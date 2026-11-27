using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.TrainingPlan;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Helpers;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Application.Services;

public class TrainingPlanService : ITrainingPlanService
{
    public static DateTime GetBusinessToday() => PlanScheduleHelper.GetBusinessToday();

    private readonly ITrainingPlanRepository _trainingPlanRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationRepository? _notifications;

    public TrainingPlanService(
        ITrainingPlanRepository trainingPlanRepository,
        IUnitOfWork unitOfWork,
        INotificationRepository? notifications = null)
    {
        _trainingPlanRepository = trainingPlanRepository;
        _unitOfWork = unitOfWork;
        _notifications = notifications;
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

        if (!dto.StartDate.HasValue)
        {
            throw new ValidationException("StartDate is required.");
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

        var packageType = orderDetail.PackageType ?? orderDetail.Package?.PackageType ?? "BOTH";
        if (string.Equals(packageType, "MEAL", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("This package is MEAL only and does not include a workout/training plan.");
        }

        // Validate plan constraints against order and package
        ValidatePlanConstraints(orderDetail, dto.StartDate, dto.EndDate, dto.Exercises);

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var plan = new TrainingPlan
        {
            TrainingPlanId = Guid.NewGuid(),
            CoachId = coachId,
            OrderDetailsId = dto.OrderDetailsId,
            OrderDetails = orderDetail,
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
        return await UpdateTrainingPlanInternalAsync(coachId, planId, dto, asOfDate: null, ct);
    }

    public async Task<TrainingPlanDetailDto> UpdateTrainingPlanInternalAsync(
        Guid coachId,
        Guid planId,
        UpdateTrainingPlanDto dto,
        DateTime? asOfDate = null,
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
        else if (existingPlan.OrderDetailsId.HasValue)
        {
            existingPlan.OrderDetails = await _trainingPlanRepository.GetOrderDetailWithOrderAndPackageAsync(existingPlan.OrderDetailsId.Value, ct);
        }

        // Validate plan constraints on update
        var effectiveStartDate = dto.StartDate ?? existingPlan.StartDate;
        var effectiveEndDate = dto.EndDate ?? existingPlan.EndDate;
        var exercisesToCheck = dto.Exercises ?? existingPlan.TrainingPlanExercises.Select(e => new TrainingPlanExerciseDto
        {
            TrainingPlanExerciseId = e.TrainingPlanExerciseId,
            ExerciseName = e.ExerciseName ?? string.Empty,
            WeekNumber = e.WeekNumber ?? 1,
            DayNumber = e.DayNumber ?? 1,
            Sets = e.Sets,
            Reps = e.Reps,
            DurationMinutes = e.DurationMinutes,
            RestSeconds = e.RestSeconds,
            Notes = e.Notes,
            SortOrder = e.SortOrder,
            VideoTutorialId = e.VideoTutorialId
        }).ToList();

        var today = (asOfDate ?? DateTime.Today).Date;

        ValidatePlanConstraints(existingPlan.OrderDetails, effectiveStartDate, effectiveEndDate, exercisesToCheck, isUpdate: true, asOfDate: today);

        // Rule: Start Date cannot be altered once training has begun / past sessions exist
        if (dto.StartDate.HasValue && existingPlan.StartDate.HasValue && dto.StartDate.Value.Date != existingPlan.StartDate.Value.Date)
        {
            var hasStartedOrPast = existingPlan.StartDate.Value.Date <= today ||
                existingPlan.TrainingPlanExercises.Any(e =>
                    GetSessionDate(existingPlan.StartDate.Value, e.WeekNumber ?? 1, e.DayNumber ?? 1).Date <= today);

            if (hasStartedOrPast)
            {
                throw new ValidationException("Cannot change Start Date once training sessions have already started or occurred.");
            }
        }

        // Rule: "đối với cả training plan có sessions hay không thì cũng không được update các buổi tập đã xảy ra và các buổi tập trong ngày hôm nay"
        if (dto.Exercises != null)
        {
            if (existingPlan.StartDate.HasValue)
            {
                var baseStart = existingPlan.StartDate.Value;
                foreach (var existingEx in existingPlan.TrainingPlanExercises)
                {
                    var sessionDate = GetSessionDate(baseStart, existingEx.WeekNumber ?? 1, existingEx.DayNumber ?? 1);
                    if (sessionDate.Date <= today)
                    {
                        var matchedIncoming = dto.Exercises.FirstOrDefault(e =>
                            e.TrainingPlanExerciseId.HasValue && e.TrainingPlanExerciseId.Value == existingEx.TrainingPlanExerciseId);

                        if (matchedIncoming == null)
                        {
                            throw new ValidationException(
                                $"Cannot delete workout session on {sessionDate:dd/MM/yyyy} ({existingEx.ExerciseName}) as it is scheduled for today or has already occurred.");
                        }

                        bool isModified =
                            !string.Equals(matchedIncoming.ExerciseName?.Trim(), existingEx.ExerciseName?.Trim(), StringComparison.Ordinal) ||
                            matchedIncoming.WeekNumber != (existingEx.WeekNumber ?? 1) ||
                            matchedIncoming.DayNumber != (existingEx.DayNumber ?? 1) ||
                            matchedIncoming.Sets != existingEx.Sets ||
                            matchedIncoming.Reps != existingEx.Reps ||
                            matchedIncoming.DurationMinutes != existingEx.DurationMinutes ||
                            matchedIncoming.RestSeconds != existingEx.RestSeconds ||
                            !string.Equals(matchedIncoming.Notes?.Trim() ?? string.Empty, existingEx.Notes?.Trim() ?? string.Empty, StringComparison.Ordinal) ||
                            (matchedIncoming.VideoTutorialId.HasValue && matchedIncoming.VideoTutorialId != existingEx.VideoTutorialId) ||
                            matchedIncoming.NewVideoTutorial != null;

                        if (isModified)
                        {
                            throw new ValidationException(
                                $"Cannot modify workout session on {sessionDate:dd/MM/yyyy} ({existingEx.ExerciseName}) as it is scheduled for today or has already occurred.");
                        }
                    }
                }
            }

            if (effectiveStartDate.HasValue)
            {
                var existingIds = existingPlan.TrainingPlanExercises
                    .Select(e => e.TrainingPlanExerciseId)
                    .ToHashSet();

                foreach (var incomingEx in dto.Exercises)
                {
                    bool isNew = !incomingEx.TrainingPlanExerciseId.HasValue ||
                                 incomingEx.TrainingPlanExerciseId.Value == Guid.Empty ||
                                 !existingIds.Contains(incomingEx.TrainingPlanExerciseId.Value);

                    if (isNew)
                    {
                        var newSessionDate = GetSessionDate(effectiveStartDate.Value, incomingEx.WeekNumber, incomingEx.DayNumber);
                        if (newSessionDate.Date <= today)
                        {
                            throw new ValidationException(
                                $"Cannot add new exercises to today or past dates ({newSessionDate:dd/MM/yyyy}). Only upcoming sessions can be scheduled.");
                        }
                    }
                }
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
            if ((string.Equals(dto.Status, "PUBLISHED", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(dto.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)) && existingPlan.PublishedAt == null)
            {
                existingPlan.PublishedAt = now;
            }
        }
        existingPlan.UpdatedAt = now;

        if (dto.Exercises != null)
        {
            // 1. Identify exercises to remove (past/today exercises deletion already guarded above)
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
                        // Skip modifying locked sessions
                        if (existingPlan.StartDate.HasValue)
                        {
                            var sDate = GetSessionDate(existingPlan.StartDate.Value, existingExercise.WeekNumber ?? 1, existingExercise.DayNumber ?? 1);
                            if (sDate.Date <= today)
                            {
                                continue;
                            }
                        }

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
            PackageDurationDays = entity.OrderDetails?.PackageDurationDays ?? entity.OrderDetails?.Package?.DurationDays,
            PackageSessionCount = entity.OrderDetails?.Package?.SessionCount,
            PackagePurchasedAt = entity.OrderDetails?.Order?.CreatedAt ?? entity.OrderDetails?.CreatedAt,
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

    private static void ValidateOrderStatusForTraineeAccess(Order? order)
        => PlanScheduleHelper.ValidateOrderStatusForTraineeAccess(order, "workout plan");

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

            ValidateOrderStatusForTraineeAccess(od.Order);

            return new TraineeWorkoutPlanProgressDto
            {
                TrainingPlanId = Guid.Empty,
                OrderDetailsId = orderDetailsId,
                PackageId = od.PackageId ?? Guid.Empty,
                PackageTitle = od.PackageTitle ?? od.Package?.Title ?? "Training Package",
                PackageType = od.PackageType ?? od.Package?.PackageType ?? "BOTH",
                PlanTitle = "Training Plan Not Ready",
                PlanDescription = "Your coach is currently creating your personalized training program. Please check back soon!",
                PlanStatus = "PENDING_COACH",
                CoachId = od.Order?.CoachId ?? od.Package?.CoachId ?? Guid.Empty,
                CoachName = od.CoachName ?? od.Package?.Coach?.Coach?.FullName ?? "Your Coach"
            };
        }

        ValidateOrderStatusForTraineeAccess(plan.OrderDetails?.Order);

        var isDraftOrPending = string.Equals(plan.Status, "DRAFT", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(plan.Status, "PENDING_COACH", StringComparison.OrdinalIgnoreCase);
        if (isDraftOrPending)
        {
            return BuildDraftProgressDto(plan);
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

        ValidateOrderStatusForTraineeAccess(plan.OrderDetails?.Order);

        var isDraftOrPending = string.Equals(plan.Status, "DRAFT", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(plan.Status, "PENDING_COACH", StringComparison.OrdinalIgnoreCase);
        if (isDraftOrPending)
        {
            return BuildDraftProgressDto(plan);
        }

        await BackfillIncompleteLogsAsync(plan, traineeId, ct);
        return MapToTraineeProgressDto(plan, traineeId);
    }

    private static TraineeWorkoutPlanProgressDto BuildDraftProgressDto(TrainingPlan plan)
    {
        return new TraineeWorkoutPlanProgressDto
        {
            TrainingPlanId = plan.TrainingPlanId,
            OrderDetailsId = plan.OrderDetailsId ?? Guid.Empty,
            PackageId = plan.OrderDetails?.PackageId ?? Guid.Empty,
            PackageTitle = plan.OrderDetails?.PackageTitle ?? plan.OrderDetails?.Package?.Title ?? "Training Package",
            PackageType = plan.OrderDetails?.PackageType ?? plan.OrderDetails?.Package?.PackageType ?? "BOTH",
            PlanTitle = plan.Title ?? "Training Plan",
            PlanDescription = plan.Description,
            PlanStatus = plan.Status,
            StartDate = plan.StartDate,
            EndDate = plan.EndDate,
            CoachId = plan.CoachId,
            CoachName = plan.Coach?.Coach?.FullName ?? "Coach",
            CoachAvatarUrl = plan.Coach?.Coach?.AvatarUrl,
            Weeks = new List<TraineeWorkoutWeekDto>()
        };
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

        var businessToday = GetBusinessToday();
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var created = false;

        foreach (var exercise in plan.TrainingPlanExercises ?? new List<TrainingPlanExercise>())
        {
            var scheduledDate = ResolveScheduledDate(plan.StartDate, plan.EndDate, exercise.WeekNumber ?? 1, exercise.DayNumber ?? 1);
            if (!scheduledDate.HasValue || scheduledDate.Value.Date >= businessToday)
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

            var newLog = new WorkoutCompletionLog
            {
                WorkoutCompletionLogId = Guid.NewGuid(),
                TraineeId = traineeId,
                TrainingPlanExerciseId = exercise.TrainingPlanExerciseId,
                LogDate = scheduledDate.Value,
                Status = "INCOMPLETE",
                CreatedAt = now
            };

            await _trainingPlanRepository.AddWorkoutLogAsync(newLog, ct);

            exercise.WorkoutCompletionLogs ??= new List<WorkoutCompletionLog>();
            exercise.WorkoutCompletionLogs.Add(newLog);

            created = true;
        }

        if (created)
        {
            try
            {
                await _unitOfWork.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Concurrent backfill inserted logs for the same day/trainee; safe to continue.
            }
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

        if (!string.IsNullOrWhiteSpace(dto.Notes) && dto.Notes.Length > 300)
        {
            throw new ValidationException("Notes cannot exceed 300 characters.");
        }

        var exercise = await _trainingPlanRepository.GetExerciseByIdAsync(dto.TrainingPlanExerciseId, ct);
        if (exercise == null)
        {
            throw new NotFoundException("Exercise not found.");
        }

        var orderBuyerId = exercise.TrainingPlan?.OrderDetails?.Order?.BuyerId;
        if (!orderBuyerId.HasValue || orderBuyerId.Value != traineeId)
        {
            throw new ForbiddenException("You are not authorized to mark this exercise.");
        }

        ValidateOrderStatusForTraineeAccess(exercise.TrainingPlan?.OrderDetails?.Order);

        var parentPlan = exercise.TrainingPlan;
        if (parentPlan != null &&
            (string.Equals(parentPlan.Status, "DRAFT", StringComparison.OrdinalIgnoreCase)
             || string.Equals(parentPlan.Status, "PENDING_COACH", StringComparison.OrdinalIgnoreCase)
             || string.Equals(parentPlan.Status, "DELETED", StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessException("This training plan is not active yet.");
        }

        // Enforce IsToday on the server: only today's scheduled exercises can be marked COMPLETED.
        // Trainees are allowed to submit a missed reason (Notes) for past INCOMPLETE exercises.
        var businessToday = GetBusinessToday();
        DateTime? scheduledDate = null;

        if (parentPlan != null && parentPlan.StartDate.HasValue)
        {
            scheduledDate = ResolveScheduledDate(parentPlan.StartDate, parentPlan.EndDate, exercise.WeekNumber ?? 1, exercise.DayNumber ?? 1);
            if (!scheduledDate.HasValue)
            {
                throw new ValidationException("This exercise is outside the scheduled training plan dates.");
            }

            if (scheduledDate.Value.Date != businessToday)
            {
                bool isUpdatingPastMissedNote = scheduledDate.Value.Date < businessToday &&
                    string.Equals(dto.Status, "INCOMPLETE", StringComparison.OrdinalIgnoreCase);

                if (!isUpdatingPastMissedNote)
                {
                    throw new ValidationException($"You can only mark exercises scheduled for today ({businessToday:dd/MM/yyyy}). This exercise is scheduled for {scheduledDate.Value:dd/MM/yyyy}.");
                }
            }
        }

        var existingLog = await _trainingPlanRepository.GetWorkoutLogAsync(dto.TrainingPlanExerciseId, traineeId, ct);
        bool isNowCompleted;
        Guid? logId = null;
        DateTime? completedAt = null;
        string? savedNotes = dto.Notes;
        bool wasAlreadyCompleted = false;

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        if (existingLog != null)
        {
            wasAlreadyCompleted = string.Equals(existingLog.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase);

            // Chặn hủy bài tập đã COMPLETED ngay ở tầng Backend
            if (wasAlreadyCompleted)
            {
                if (string.Equals(dto.Status, "INCOMPLETE", StringComparison.OrdinalIgnoreCase) ||
                    (string.IsNullOrWhiteSpace(dto.Status) && string.IsNullOrWhiteSpace(dto.Notes)))
                {
                    throw new ValidationException("Completed exercises cannot be uncompleted or marked incomplete.");
                }

                // Cập nhật TraineeNotes cho bài tập đã hoàn thành nếu có
                if (dto.Notes != null)
                {
                    existingLog.Notes = dto.Notes;
                }
                isNowCompleted = true;
                logId = existingLog.WorkoutCompletionLogId;
                completedAt = existingLog.CreatedAt;
                savedNotes = existingLog.Notes;
            }
            else
            {
                // existingLog có trạng thái INCOMPLETE
                if (string.Equals(dto.Status, "INCOMPLETE", StringComparison.OrdinalIgnoreCase))
                {
                    if (dto.Notes != null) existingLog.Notes = dto.Notes;
                    isNowCompleted = false;
                    logId = existingLog.WorkoutCompletionLogId;
                    savedNotes = existingLog.Notes;
                }
                else
                {
                    // Chuyển sang COMPLETED
                    existingLog.Status = "COMPLETED";
                    if (dto.Notes != null) existingLog.Notes = dto.Notes;
                    existingLog.CreatedAt = now;
                    isNowCompleted = true;
                    logId = existingLog.WorkoutCompletionLogId;
                    completedAt = existingLog.CreatedAt;
                    savedNotes = existingLog.Notes;
                }
            }
        }
        else
        {
            if (string.Equals(dto.Status, "INCOMPLETE", StringComparison.OrdinalIgnoreCase))
            {
                var newLog = new WorkoutCompletionLog
                {
                    WorkoutCompletionLogId = Guid.NewGuid(),
                    TraineeId = traineeId,
                    TrainingPlanExerciseId = dto.TrainingPlanExerciseId,
                    LogDate = scheduledDate ?? now,
                    Status = "INCOMPLETE",
                    Notes = dto.Notes,
                    CreatedAt = now
                };

                await _trainingPlanRepository.AddWorkoutLogAsync(newLog, ct);
                isNowCompleted = false;
                logId = newLog.WorkoutCompletionLogId;
                savedNotes = newLog.Notes;
            }
            else
            {
                var newLog = new WorkoutCompletionLog
                {
                    WorkoutCompletionLogId = Guid.NewGuid(),
                    TraineeId = traineeId,
                    TrainingPlanExerciseId = dto.TrainingPlanExerciseId,
                    LogDate = scheduledDate ?? now,
                    Status = "COMPLETED",
                    Notes = dto.Notes,
                    CreatedAt = now
                };

                await _trainingPlanRepository.AddWorkoutLogAsync(newLog, ct);
                isNowCompleted = true;
                logId = newLog.WorkoutCompletionLogId;
                completedAt = newLog.CreatedAt;
                savedNotes = newLog.Notes;
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);

        // Fetch refreshed progress for the parent plan
        var plan = await _trainingPlanRepository.GetTraineePlanWithLogsAsync(
            exercise.TrainingPlan?.OrderDetailsId ?? Guid.Empty, traineeId, ct);

        int totalCount = plan?.TrainingPlanExercises?.Count ?? 0;
        int completedCount = plan?.TrainingPlanExercises?
            .Count(e => e.WorkoutCompletionLogs?.Any(l => l.TraineeId == traineeId && l.Status == "COMPLETED") == true) ?? 0;
        double percentage = totalCount > 0 ? Math.Round((double)completedCount / totalCount * 100, 1) : 0;

        bool isPlanFullyCompleted = totalCount > 0 && completedCount >= totalCount;

        // Auto update TrainingPlan.Status to COMPLETED when 100% completed
        if (isPlanFullyCompleted && parentPlan != null && !string.Equals(parentPlan.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
        {
            parentPlan.Status = "COMPLETED";
            parentPlan.UpdatedAt = now;
            await _unitOfWork.SaveChangesAsync(ct);
        }

        // Send notifications to Coach when daily workout or full program is completed
        if (isNowCompleted && !wasAlreadyCompleted && _notifications != null && parentPlan != null && parentPlan.CoachId != Guid.Empty)
        {
            var coachId = parentPlan.CoachId;
            var traineeName = parentPlan.OrderDetails?.Order?.Buyer?.FullName ?? "Trainee";
            var packageTitle = parentPlan.OrderDetails?.PackageTitle ?? parentPlan.OrderDetails?.Package?.Title ?? parentPlan.Title ?? "Training Package";

            if (isPlanFullyCompleted)
            {
                await _notifications.AddAsync(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = coachId,
                    ActorId = traineeId,
                    ReferenceId = parentPlan.TrainingPlanId,
                    Type = "TRAINING_PLAN_COMPLETED",
                    Description = $"Trainee {traineeName} has completed 100% of the training plan for package \"{packageTitle}\".",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                }, ct);
            }

            var dayExercises = plan?.TrainingPlanExercises?
                .Where(e => e.WeekNumber == (exercise.WeekNumber ?? 1) && e.DayNumber == (exercise.DayNumber ?? 1))
                .ToList();

            bool isDayFullyCompleted = dayExercises != null && dayExercises.Count > 0 &&
                dayExercises.All(e => e.WorkoutCompletionLogs?.Any(l => l.TraineeId == traineeId && l.Status == "COMPLETED") == true);

            if (isDayFullyCompleted && !isPlanFullyCompleted)
            {
                await _notifications.AddAsync(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = coachId,
                    ActorId = traineeId,
                    ReferenceId = parentPlan.TrainingPlanId,
                    Type = "WORKOUT_DAY_COMPLETED",
                    Description = $"Trainee {traineeName} has completed the daily workout (Week {exercise.WeekNumber ?? 1}, Day {exercise.DayNumber ?? 1}) for package \"{packageTitle}\".",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                }, ct);
            }

            await _unitOfWork.SaveChangesAsync(ct);
        }

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

    public async Task<WorkoutCompletionResultDto> RequestWorkoutCompletionAsync(
        Guid traineeId,
        RequestWorkoutCompletionDto dto,
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

        if (!string.IsNullOrWhiteSpace(dto.Notes) && dto.Notes.Length > 300)
        {
            throw new ValidationException("Notes cannot exceed 300 characters.");
        }

        if (!string.IsNullOrWhiteSpace(dto.MediaUrl) && dto.MediaUrl.Length > 500)
        {
            throw new ValidationException("MediaUrl cannot exceed 500 characters.");
        }

        var exercise = await _trainingPlanRepository.GetExerciseByIdAsync(dto.TrainingPlanExerciseId, ct);
        if (exercise == null)
        {
            throw new NotFoundException("Exercise not found.");
        }

        var orderBuyerId = exercise.TrainingPlan?.OrderDetails?.Order?.BuyerId;
        if (!orderBuyerId.HasValue || orderBuyerId.Value != traineeId)
        {
            throw new ForbiddenException("You are not authorized to submit completion for this exercise.");
        }

        ValidateOrderStatusForTraineeAccess(exercise.TrainingPlan?.OrderDetails?.Order);

        var parentPlan = exercise.TrainingPlan;
        if (parentPlan != null &&
            (string.Equals(parentPlan.Status, "DRAFT", StringComparison.OrdinalIgnoreCase)
             || string.Equals(parentPlan.Status, "PENDING_COACH", StringComparison.OrdinalIgnoreCase)
             || string.Equals(parentPlan.Status, "DELETED", StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessException("This training plan is not active yet.");
        }

        var businessToday = GetBusinessToday();
        DateTime? scheduledDate = null;

        if (parentPlan != null && parentPlan.StartDate.HasValue)
        {
            scheduledDate = ResolveScheduledDate(parentPlan.StartDate, parentPlan.EndDate, exercise.WeekNumber ?? 1, exercise.DayNumber ?? 1);
            if (!scheduledDate.HasValue)
            {
                throw new ValidationException("This exercise is outside the scheduled training plan dates.");
            }

            if (scheduledDate.Value.Date != businessToday)
            {
                throw new ValidationException($"You can only complete exercises scheduled for today ({businessToday:dd/MM/yyyy}). This exercise is scheduled for {scheduledDate.Value:dd/MM/yyyy}.");
            }
        }

        var existingLog = await _trainingPlanRepository.GetWorkoutLogAsync(dto.TrainingPlanExerciseId, traineeId, ct);
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        Guid logId;
        string? savedNotes = dto.Notes?.Trim();
        string? mediaUrl = dto.MediaUrl?.Trim();

        if (existingLog != null)
        {
            if (string.Equals(existingLog.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException("This exercise has already been completed and approved by coach.");
            }

            existingLog.Status = "PENDING_REVIEW";
            existingLog.MediaUrl = mediaUrl;
            if (savedNotes != null) existingLog.Notes = savedNotes;
            existingLog.CoachFeedback = null;
            existingLog.ReviewedBy = null;
            existingLog.ReviewedAt = null;
            existingLog.CreatedAt = now;
            logId = existingLog.WorkoutCompletionLogId;
        }
        else
        {
            var newLog = new WorkoutCompletionLog
            {
                WorkoutCompletionLogId = Guid.NewGuid(),
                TraineeId = traineeId,
                TrainingPlanExerciseId = dto.TrainingPlanExerciseId,
                LogDate = scheduledDate ?? now,
                Status = "PENDING_REVIEW",
                MediaUrl = mediaUrl,
                Notes = savedNotes,
                CreatedAt = now
            };

            await _trainingPlanRepository.AddWorkoutLogAsync(newLog, ct);
            logId = newLog.WorkoutCompletionLogId;
        }

        await _unitOfWork.SaveChangesAsync(ct);

        // Send notification to Coach
        if (_notifications != null && parentPlan != null && parentPlan.CoachId != Guid.Empty)
        {
            var coachId = parentPlan.CoachId;
            var traineeName = parentPlan.OrderDetails?.Order?.Buyer?.FullName ?? "Trainee";
            var packageTitle = parentPlan.OrderDetails?.PackageTitle ?? parentPlan.OrderDetails?.Package?.Title ?? parentPlan.Title ?? "Training Package";
            var exerciseName = exercise.ExerciseName ?? "Exercise";

            await _notifications.AddAsync(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = coachId,
                ActorId = traineeId,
                ReferenceId = parentPlan.TrainingPlanId,
                Type = "WORKOUT_COMPLETION_REQUESTED",
                Description = $"Trainee {traineeName} submitted completion proof for workout \"{exerciseName}\" (Week {exercise.WeekNumber ?? 1}, Day {exercise.DayNumber ?? 1}) for package \"{packageTitle}\".",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            }, ct);

            await _unitOfWork.SaveChangesAsync(ct);
        }

        // Fetch refreshed progress
        var plan = await _trainingPlanRepository.GetTraineePlanWithLogsAsync(
            exercise.TrainingPlan?.OrderDetailsId ?? Guid.Empty, traineeId, ct);

        int totalCount = plan?.TrainingPlanExercises?.Count ?? 0;
        int completedCount = plan?.TrainingPlanExercises?
            .Count(e => e.WorkoutCompletionLogs?.Any(l => l.TraineeId == traineeId && l.Status == "COMPLETED") == true) ?? 0;
        double percentage = totalCount > 0 ? Math.Round((double)completedCount / totalCount * 100, 1) : 0;

        return new WorkoutCompletionResultDto
        {
            TrainingPlanExerciseId = dto.TrainingPlanExerciseId,
            IsCompleted = false,
            WorkoutCompletionLogId = logId,
            CompletedAt = null,
            Status = "PENDING_REVIEW",
            Notes = savedNotes,
            MediaUrl = mediaUrl,
            CoachFeedback = null,
            ReviewedAt = null,
            TotalExercisesCount = totalCount,
            CompletedExercisesCount = completedCount,
            ProgressPercentage = percentage
        };
    }

    public async Task<WorkoutCompletionResultDto> ReviewWorkoutCompletionAsync(
        Guid coachId,
        CoachReviewWorkoutDto dto,
        CancellationToken ct = default)
    {
        if (coachId == Guid.Empty)
        {
            throw new ForbiddenException("Invalid coach identity.");
        }

        if (dto == null || dto.TrainingPlanExerciseId == Guid.Empty)
        {
            throw new ValidationException("TrainingPlanExerciseId is required.");
        }

        if (dto.TraineeId == Guid.Empty)
        {
            throw new ValidationException("TraineeId is required.");
        }

        var normalizedDecision = dto.Decision?.Trim().ToUpperInvariant();
        if (normalizedDecision != "COMPLETED" && normalizedDecision != "UNCOMPLETE")
        {
            throw new ValidationException("Decision must be either 'COMPLETED' or 'UNCOMPLETE'.");
        }

        var feedback = dto.Feedback?.Trim();
        if (normalizedDecision == "UNCOMPLETE" && string.IsNullOrWhiteSpace(feedback))
        {
            throw new ValidationException("Feedback is required when marking a workout as uncomplete so the trainee knows what to improve.");
        }

        if (!string.IsNullOrWhiteSpace(feedback) && feedback.Length > 500)
        {
            throw new ValidationException("Feedback cannot exceed 500 characters.");
        }

        var exercise = await _trainingPlanRepository.GetExerciseByIdAsync(dto.TrainingPlanExerciseId, ct);
        if (exercise == null)
        {
            throw new NotFoundException("Exercise not found.");
        }

        var parentPlan = exercise.TrainingPlan;
        var isCoachOwner = parentPlan != null && (
            parentPlan.CoachId == coachId ||
            parentPlan.OrderDetails?.Order?.CoachId == coachId ||
            parentPlan.OrderDetails?.Package?.CoachId == coachId);
        if (!isCoachOwner || parentPlan == null)
        {
            throw new ForbiddenException("You are not authorized to review exercises for this training plan.");
        }

        var orderBuyerId = parentPlan.OrderDetails?.Order?.BuyerId;
        if (!orderBuyerId.HasValue || orderBuyerId.Value != dto.TraineeId)
        {
            throw new ForbiddenException("You are not authorized to review exercises for this trainee.");
        }

        ValidateOrderStatusForTraineeAccess(parentPlan.OrderDetails?.Order);

        var existingLog = await _trainingPlanRepository.GetWorkoutLogAsync(dto.TrainingPlanExerciseId, dto.TraineeId, ct);
        if (existingLog == null || string.Equals(existingLog.Status, "INCOMPLETE", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("Trainee has not submitted a completion request for this exercise yet.");
        }

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        existingLog.Status = normalizedDecision;
        existingLog.CoachFeedback = feedback;
        existingLog.ReviewedBy = coachId;
        existingLog.ReviewedAt = now;
        Guid logId = existingLog.WorkoutCompletionLogId;

        await _unitOfWork.SaveChangesAsync(ct);

        // Fetch refreshed progress for the parent plan
        var plan = await _trainingPlanRepository.GetTraineePlanWithLogsAsync(
            parentPlan.OrderDetailsId ?? Guid.Empty, dto.TraineeId, ct);

        var scheduledExercises = (plan?.TrainingPlanExercises ?? new List<TrainingPlanExercise>())
            .Where(e => !plan!.StartDate.HasValue ||
                        ResolveScheduledDate(plan.StartDate, plan.EndDate, e.WeekNumber ?? 1, e.DayNumber ?? 1).HasValue)
            .ToList();

        int totalCount = scheduledExercises.Count;
        int completedCount = scheduledExercises
            .Count(e => e.WorkoutCompletionLogs?.Any(l => l.TraineeId == dto.TraineeId && string.Equals(l.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase)) == true);
        double percentage = totalCount > 0 ? Math.Round((double)completedCount / totalCount * 100, 1) : 0;
        bool isPlanFullyCompleted = totalCount > 0 && completedCount >= totalCount;

        // Auto update TrainingPlan.Status to COMPLETED when 100% completed, or revert if rejected below 100%
        if (isPlanFullyCompleted && !string.Equals(parentPlan.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
        {
            parentPlan.Status = "COMPLETED";
            parentPlan.UpdatedAt = now;
            await _unitOfWork.SaveChangesAsync(ct);
        }
        else if (!isPlanFullyCompleted && string.Equals(parentPlan.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
        {
            parentPlan.Status = "IN_PROGRESS";
            parentPlan.UpdatedAt = now;
            await _unitOfWork.SaveChangesAsync(ct);
        }

        // Send Notification to Trainee
        if (_notifications != null)
        {
            var coachName = parentPlan?.Coach?.Coach?.FullName ?? "Coach";
            var exerciseName = exercise.ExerciseName ?? "Exercise";
            var isApproved = normalizedDecision == "COMPLETED";

            var notifDesc = isApproved
                ? (string.IsNullOrWhiteSpace(feedback)
                    ? $"Coach {coachName} approved workout \"{exerciseName}\"."
                    : $"Coach {coachName} approved workout \"{exerciseName}\". Feedback: \"{feedback}\"")
                : $"Coach {coachName} reviewed workout \"{exerciseName}\" as incomplete. Feedback: \"{feedback}\"";

            await _notifications.AddAsync(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = dto.TraineeId,
                ActorId = coachId,
                ReferenceId = parentPlan?.TrainingPlanId ?? exercise.TrainingPlanId,
                Type = isApproved ? "WORKOUT_APPROVED" : "WORKOUT_REJECTED",
                Description = notifDesc,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            }, ct);

            await _unitOfWork.SaveChangesAsync(ct);
        }

        return new WorkoutCompletionResultDto
        {
            TrainingPlanExerciseId = dto.TrainingPlanExerciseId,
            IsCompleted = normalizedDecision == "COMPLETED",
            WorkoutCompletionLogId = logId,
            CompletedAt = normalizedDecision == "COMPLETED" ? existingLog.CreatedAt : null,
            Status = normalizedDecision,
            Notes = existingLog.Notes,
            MediaUrl = existingLog.MediaUrl,
            CoachFeedback = existingLog.CoachFeedback,
            ReviewedAt = existingLog.ReviewedAt,
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
            var isOwner = od != null && (
                (od.Order != null && od.Order.CoachId == coachId) ||
                (od.Package != null && od.Package.CoachId == coachId));

            if (!isOwner || od == null)
            {
                throw new NotFoundException("Order detail not found or does not belong to you.");
            }

            var packageType = od.PackageType ?? od.Package?.PackageType ?? "BOTH";
            if (string.Equals(packageType, "MEAL", StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException("This package is MEAL only and does not include a workout/training plan.");
            }

            ValidateOrderStatusForTraineeAccess(od.Order);

            return new CoachTraineeWorkoutProgressDto
            {
                OrderDetailsId = orderDetailsId,
                TraineeId = od.Order?.BuyerId ?? Guid.Empty,
                TraineeName = od.Order?.Buyer?.FullName ?? "Trainee",
                TraineeEmail = od.Order?.Buyer?.Email ?? string.Empty,
                PackageId = od.PackageId ?? Guid.Empty,
                PackageTitle = od.PackageTitle ?? od.Package?.Title ?? "Training Package",
                PlanTitle = "No Plan Created",
                PlanStatus = "DRAFT",
                TotalExercisesCount = 0,
                CompletedExercisesCount = 0,
                MissedExercisesCount = 0,
                AdherenceRate = 0
            };
        }

        var planPackageType = plan.OrderDetails?.PackageType ?? plan.OrderDetails?.Package?.PackageType ?? "BOTH";
        if (string.Equals(planPackageType, "MEAL", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("This package is MEAL only and does not include a workout/training plan.");
        }

        // Validate order status: Chặn nếu REFUNDED, CANCELLED, PENDING...
        ValidateOrderStatusForTraineeAccess(plan.OrderDetails?.Order);

        var buyer = plan.OrderDetails?.Order?.Buyer;
        var buyerUserId = buyer?.UserId ?? plan.OrderDetails?.Order?.BuyerId;

        // Tự động Backfill Incomplete khi Coach mở màn hình
        var isDraftOrPending = string.Equals(plan.Status, "DRAFT", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(plan.Status, "PENDING_COACH", StringComparison.OrdinalIgnoreCase);
        if (!isDraftOrPending && buyerUserId.HasValue)
        {
            await BackfillIncompleteLogsAsync(plan, buyerUserId.Value, ct);
        }

        var weeks = BuildWeeksBreakdown(plan.TrainingPlanExercises, buyerUserId, plan.StartDate, plan.EndDate);

        var allLogs = plan.TrainingPlanExercises
            .SelectMany(e => (e.WorkoutCompletionLogs ?? new List<WorkoutCompletionLog>())
                .Where(l => !buyerUserId.HasValue || l.TraineeId == buyerUserId.Value)
                .Select(l => new { Exercise = e, Log = l }))
            .OrderByDescending(x => x.Log.CreatedAt)
            .Take(50)
            .Select(x => new CoachWorkoutActivityLogDto
            {
                LogId = x.Log.WorkoutCompletionLogId,
                TrainingPlanExerciseId = x.Exercise.TrainingPlanExerciseId,
                ExerciseName = x.Exercise.ExerciseName ?? "Exercise",
                WeekNumber = x.Exercise.WeekNumber,
                DayNumber = x.Exercise.DayNumber,
                CompletedAt = x.Log.CreatedAt ?? DateTime.UtcNow,
                Status = x.Log.Status,
                Notes = x.Log.Notes,
                MediaUrl = x.Log.MediaUrl,
                CoachFeedback = x.Log.CoachFeedback,
                ReviewedAt = x.Log.ReviewedAt
            })
            .ToList();

        int totalCount = weeks.Sum(w => w.TotalExercises);
        int completedCount = weeks.Sum(w => w.CompletedExercises);
        int missedCount = weeks.Sum(w => w.MissedExercises);
        double adherenceRate = PlanScheduleHelper.CalculateAdherenceRate(completedCount, missedCount, totalCount);

        return new CoachTraineeWorkoutProgressDto
        {
            OrderDetailsId = orderDetailsId,
            TraineeId = buyerUserId ?? Guid.Empty,
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
            MissedExercisesCount = missedCount,
            AdherenceRate = adherenceRate,
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
        int missedCount = weeks.Sum(w => w.MissedExercises);
        double adherenceRate = PlanScheduleHelper.CalculateAdherenceRate(completedCount, missedCount, totalCount);

        return new TraineeWorkoutPlanProgressDto
        {
            TrainingPlanId = plan.TrainingPlanId,
            OrderDetailsId = plan.OrderDetailsId ?? Guid.Empty,
            PackageId = plan.OrderDetails?.PackageId ?? Guid.Empty,
            PackageTitle = plan.OrderDetails?.PackageTitle ?? plan.OrderDetails?.Package?.Title ?? "Training Package",
            PackageThumbnailUrl = plan.OrderDetails?.Package?.Media?.OrderBy(m => m.SortOrder).FirstOrDefault()?.MediaUrl,
            PackageType = plan.OrderDetails?.PackageType ?? plan.OrderDetails?.Package?.PackageType ?? "BOTH",
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
            MissedExercisesCount = missedCount,
            AdherenceRate = adherenceRate,
            Weeks = weeks
        };
    }

    private static List<TraineeWorkoutWeekDto> BuildWeeksBreakdown(
        IEnumerable<TrainingPlanExercise> exercises,
        Guid? filterTraineeId,
        DateTime? planStartDate = null,
        DateTime? planEndDate = null)
    {
        var businessToday = GetBusinessToday();
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
                    IsToday = !hasSchedule || (scheduledDate.HasValue && scheduledDate.Value.Date == businessToday)
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
                        MediaUrl = relevantLog?.MediaUrl,
                        CoachFeedback = relevantLog?.CoachFeedback,
                        ReviewedAt = relevantLog?.ReviewedAt,
                        Status = relevantLog?.Status,
                        ScheduledDate = scheduledDate
                    });
                }

                dayDto.TotalExercises = dayDto.Exercises.Count;
                dayDto.CompletedExercises = dayDto.Exercises.Count(e => e.IsCompleted);
                dayDto.MissedExercises = dayDto.Exercises.Count(e => string.Equals(e.Status, "INCOMPLETE", StringComparison.OrdinalIgnoreCase));

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
            weekDto.MissedExercises = weekDto.Days.Sum(d => d.MissedExercises);
            weeks.Add(weekDto);
        }

        return weeks;
    }

    public static DateTime GetStartMonday(DateTime startDate)
        => PlanScheduleHelper.GetStartMonday(startDate);

    public static DateTime GetSessionDate(DateTime startDate, short weekNumber, short dayNumber)
        => PlanScheduleHelper.GetSessionDate(startDate, weekNumber, dayNumber);

    public static void ValidatePlanConstraints(
        OrderDetail? orderDetail,
        DateTime? startDate,
        DateTime? endDate,
        IEnumerable<TrainingPlanExerciseDto>? exercises,
        bool isUpdate = false,
        DateTime? asOfDate = null)
    {
        var exerciseList = exercises?.ToList() ?? new List<TrainingPlanExerciseDto>();
        var sessionCount = orderDetail?.Package?.SessionCount;
        var scheduledSessionsCount = exerciseList
            .Where(e => !string.IsNullOrWhiteSpace(e.ExerciseName))
            .Select(e => new { e.WeekNumber, e.DayNumber })
            .Distinct()
            .Count();

        if (startDate.HasValue && endDate.HasValue)
        {
            if (endDate.Value.Date < startDate.Value.Date)
            {
                throw new ValidationException("End Date must be on or after Start Date.");
            }

            var durationDays = orderDetail?.PackageDurationDays ?? orderDetail?.Package?.DurationDays;
            var packagePurchasedAt = orderDetail?.Order?.CreatedAt ?? orderDetail?.CreatedAt;

            if (packagePurchasedAt.HasValue && packagePurchasedAt.Value != default(DateTime))
            {
                var packageStartDate = packagePurchasedAt.Value.Date;
                if (startDate.Value.Date < packageStartDate)
                {
                    throw new ValidationException($"Start Date ({startDate.Value:dd/MM/yyyy}) cannot be earlier than package purchase date ({packageStartDate:dd/MM/yyyy}).");
                }

                if (durationDays.HasValue && durationDays.Value > 0)
                {
                    var packageEndDate = packageStartDate.AddDays(durationDays.Value);
                    if (endDate.Value.Date > packageEndDate)
                    {
                        throw new ValidationException($"End Date ({endDate.Value:dd/MM/yyyy}) cannot exceed package expiration date ({packageEndDate:dd/MM/yyyy}).");
                    }
                }
            }

            if (durationDays.HasValue && durationDays.Value > 0)
            {
                var planDays = (int)(endDate.Value.Date - startDate.Value.Date).TotalDays + 1;
                if (planDays > durationDays.Value + 1)
                {
                    throw new ValidationException($"Training plan duration ({planDays} days) cannot exceed purchased package duration ({durationDays.Value} days).");
                }
            }

            // Week limit: "nếu như khoảng start day và end day là 1 tháng thì không được add số tuần nhiều hơn 1 tháng."
            var totalDays = (int)(endDate.Value.Date - startDate.Value.Date).TotalDays + 1;
            var maxWeeks = Math.Max(1, totalDays / 7);

            if (exerciseList.Any(e => e.WeekNumber > maxWeeks))
            {
                var highest = exerciseList.Max(e => e.WeekNumber);
                throw new ValidationException($"Training plan cannot exceed {maxWeeks} weeks for the selected {totalDays}-day period (highest week found: {highest}).");
            }

            // Upcoming week schedule requirement:
            // "không được để trống lịch tập trong tuần tiếp theo"
            var effectiveDate = (asOfDate ?? GetBusinessToday()).Date;
            var startMonday = GetStartMonday(startDate.Value.Date);

            short upcomingWeek;
            if (!isUpdate || effectiveDate < startMonday)
            {
                upcomingWeek = 1;
            }
            else
            {
                var currentWeek = (short)(((effectiveDate - startMonday).Days / 7) + 1);
                upcomingWeek = (short)(currentWeek + 1);
            }

            bool allSessionsAlreadyUsed = sessionCount.HasValue && sessionCount.Value > 0 &&
                                          scheduledSessionsCount >= sessionCount.Value;

            if (upcomingWeek <= maxWeeks && (!allSessionsAlreadyUsed || upcomingWeek == 1))
            {
                var hasUpcomingWeekSession = exerciseList.Any(e =>
                    e.WeekNumber == upcomingWeek && !string.IsNullOrWhiteSpace(e.ExerciseName));

                if (!hasUpcomingWeekSession)
                {
                    var weekLabel = upcomingWeek == 1
                        ? "Week 1 (starting week)"
                        : $"Week {upcomingWeek} (the upcoming week)";

                    throw new ValidationException(
                        $"Schedule for {weekLabel} cannot be empty. Please ensure workouts are scheduled for the upcoming week.");
                }
            }
        }

        // Session count constraint:
        // Coach can schedule phase-by-phase (e.g. upload first week only without needing to upload full upfront).
        // Total scheduled workout sessions must not exceed the package session count.
        if (sessionCount.HasValue && sessionCount.Value > 0 && exerciseList.Count > 0)
        {
            if (scheduledSessionsCount > sessionCount.Value)
            {
                throw new ValidationException($"The number of scheduled workout sessions ({scheduledSessionsCount}) cannot exceed the package session count ({sessionCount.Value} sessions).");
            }
        }
    }

    /// <summary>
    /// Maps a (WeekNumber, DayNumber) pair onto a real calendar date using the plan StartDate's Monday.
    /// Matches exactly how coaches see days in CoachTrainingPlanFormModal (Day 1 = Mon ... Day 7 = Sun).
    /// Returns null when the plan has no StartDate or the date exceeds plan EndDate.
    /// </summary>
    private static DateTime? ResolveScheduledDate(DateTime? planStartDate, DateTime? planEndDate, short weekNumber, short dayNumber)
        => PlanScheduleHelper.ResolveScheduledDate(planStartDate, planEndDate, weekNumber, dayNumber);
}
