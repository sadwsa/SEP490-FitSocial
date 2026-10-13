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
            if (string.Equals(dto.Status, "PUBLISHED", StringComparison.OrdinalIgnoreCase) && existingPlan.PublishedAt == null)
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

    public static DateTime GetStartMonday(DateTime startDate)
    {
        int dayOfWeek = (int)startDate.DayOfWeek;
        int daysSinceMonday = (dayOfWeek == 0 ? 7 : dayOfWeek) - 1;
        return startDate.Date.AddDays(-daysSinceMonday);
    }

    public static DateTime GetSessionDate(DateTime startDate, short weekNumber, short dayNumber)
    {
        var startMonday = GetStartMonday(startDate);
        return startMonday.AddDays((weekNumber - 1) * 7 + (dayNumber - 1));
    }

    public static void ValidatePlanConstraints(
        OrderDetail? orderDetail,
        DateTime? startDate,
        DateTime? endDate,
        IEnumerable<TrainingPlanExerciseDto>? exercises,
        bool isUpdate = false,
        DateTime? asOfDate = null)
    {
        var exerciseList = exercises?.ToList() ?? new List<TrainingPlanExerciseDto>();

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
            var effectiveDate = (asOfDate ?? DateTime.Today).Date;
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

            if (upcomingWeek <= maxWeeks)
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
        // "đối với các training package có số lượng ngày quá lớn cho những chương trình dài hạn thì cho phép họ để null sessions như là flexible"
        var sessionCount = orderDetail?.Package?.SessionCount;
        if (sessionCount.HasValue && sessionCount.Value > 0 && exerciseList.Count > 0)
        {
            var scheduledSessions = exerciseList
                .Where(e => !string.IsNullOrWhiteSpace(e.ExerciseName))
                .Select(e => new { e.WeekNumber, e.DayNumber })
                .Distinct()
                .Count();

            if (scheduledSessions != sessionCount.Value)
            {
                throw new ValidationException($"The number of scheduled workout sessions ({scheduledSessions}) must match the package session count ({sessionCount.Value} sessions).");
            }
        }
    }
}
