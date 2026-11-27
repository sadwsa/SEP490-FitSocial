using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.API.Controllers;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.TrainingPlan;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using FitSocial.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

/// <summary>
/// Unit test suite for Request Training Completion & Coach Review feature:
/// 1. TraineeWorkoutController.RequestWorkoutCompletion (8 tests: 3 N, 2 B, 3 A)
/// 2. TrainingPlanService.RequestWorkoutCompletionAsync (16 tests: 4 N, 5 B, 7 A)
/// 3. CoachTrainingPlansController.ReviewWorkout (8 tests: 3 N, 2 B, 3 A)
/// 4. TrainingPlanService.ReviewWorkoutCompletionAsync (15 tests: 5 N, 3 B, 7 A)
/// 5. TrainingPlanRepository.GetExerciseByIdAsync (5 tests: 2 N, 2 B, 1 A)
/// </summary>
public class RequestTrainingCompletionServiceTests
{
    private readonly Mock<ITrainingPlanRepository> _mockPlanRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<INotificationRepository> _mockNotificationRepo;
    private readonly TrainingPlanService _service;

    public RequestTrainingCompletionServiceTests()
    {
        _mockPlanRepo = new Mock<ITrainingPlanRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockNotificationRepo = new Mock<INotificationRepository>();

        _service = new TrainingPlanService(
            _mockPlanRepo.Object,
            _mockUow.Object,
            _mockNotificationRepo.Object);
    }

    #region Helper Factories

    private static (TrainingPlan Plan, TrainingPlanExercise Exercise) CreateExerciseHierarchy(
        Guid traineeId,
        Guid? coachId = null,
        DateTime? planStartDate = null,
        DateTime? planEndDate = null,
        short weekNumber = 1,
        short dayNumber = 1,
        string orderStatus = "PAID",
        string planStatus = "IN_PROGRESS",
        Guid? exerciseId = null)
    {
        var exId = exerciseId ?? Guid.NewGuid();
        var cId = coachId ?? Guid.NewGuid();
        var orderDetailsId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var buyer = new User
        {
            UserId = traineeId,
            FullName = "Trainee Nguyen",
            Email = "trainee@fitsocial.com"
        };

        var coachUser = new User
        {
            UserId = cId,
            FullName = "Coach Tran",
            Email = "coach@fitsocial.com"
        };

        var order = new Order
        {
            OrderId = orderId,
            BuyerId = traineeId,
            Buyer = buyer,
            CoachId = cId,
            OrderStatus = orderStatus
        };

        var orderDetail = new OrderDetail
        {
            OrderDetailsId = orderDetailsId,
            OrderId = orderId,
            Order = order,
            PackageTitle = "VIP Bodybuilding 30 Days"
        };

        var coachProfile = new CoachProfile
        {
            CoachId = cId,
            Coach = coachUser,
            Status = "ACTIVE"
        };

        var plan = new TrainingPlan
        {
            TrainingPlanId = Guid.NewGuid(),
            CoachId = cId,
            Coach = coachProfile,
            OrderDetailsId = orderDetailsId,
            OrderDetails = orderDetail,
            StartDate = planStartDate,
            EndDate = planEndDate,
            Status = planStatus,
            Title = "30-Day Strength Plan",
            TrainingPlanExercises = new List<TrainingPlanExercise>()
        };

        var exercise = new TrainingPlanExercise
        {
            TrainingPlanExerciseId = exId,
            TrainingPlanId = plan.TrainingPlanId,
            TrainingPlan = plan,
            ExerciseName = "Barbell Bench Press",
            WeekNumber = weekNumber,
            DayNumber = dayNumber,
            WorkoutCompletionLogs = new List<WorkoutCompletionLog>()
        };

        plan.TrainingPlanExercises.Add(exercise);

        return (plan, exercise);
    }

    #endregion

    #region Trainee: RequestWorkoutCompletionAsync Tests

    [Fact]
    public async Task UTCID01_RequestWorkoutCompletion_Success_SetsPendingReview_SendsNotificationToCoach()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var (plan, exercise) = CreateExerciseHierarchy(traineeId, coachId);

        _mockPlanRepo.Setup(r => r.GetExerciseByIdAsync(exercise.TrainingPlanExerciseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exercise);

        _mockPlanRepo.Setup(r => r.GetWorkoutLogAsync(exercise.TrainingPlanExerciseId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkoutCompletionLog?)null);

        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(plan.OrderDetailsId.Value, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var dto = new RequestWorkoutCompletionDto
        {
            TrainingPlanExerciseId = exercise.TrainingPlanExerciseId,
            MediaUrl = "https://res.cloudinary.com/demo/video/upload/workout_proof.mp4",
            Notes = "Completed 4 sets of 50kg, form felt solid."
        };

        // Act
        var result = await _service.RequestWorkoutCompletionAsync(traineeId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsCompleted);
        Assert.Equal("PENDING_REVIEW", result.Status);
        Assert.Equal(dto.MediaUrl, result.MediaUrl);
        Assert.Equal(dto.Notes, result.Notes);

        _mockPlanRepo.Verify(r => r.AddWorkoutLogAsync(It.Is<WorkoutCompletionLog>(l =>
            l.Status == "PENDING_REVIEW" &&
            l.MediaUrl == dto.MediaUrl &&
            l.Notes == dto.Notes &&
            l.TraineeId == traineeId), It.IsAny<CancellationToken>()), Times.Once);

        _mockNotificationRepo.Verify(n => n.AddAsync(It.Is<Notification>(notif =>
            notif.UserId == coachId &&
            notif.ActorId == traineeId &&
            notif.Type == "WORKOUT_COMPLETION_REQUESTED"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID02_RequestWorkoutCompletion_AlreadyCompleted_ThrowsBusinessException()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var (plan, exercise) = CreateExerciseHierarchy(traineeId);

        var existingCompletedLog = new WorkoutCompletionLog
        {
            WorkoutCompletionLogId = Guid.NewGuid(),
            TrainingPlanExerciseId = exercise.TrainingPlanExerciseId,
            TraineeId = traineeId,
            Status = "COMPLETED"
        };

        _mockPlanRepo.Setup(r => r.GetExerciseByIdAsync(exercise.TrainingPlanExerciseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exercise);

        _mockPlanRepo.Setup(r => r.GetWorkoutLogAsync(exercise.TrainingPlanExerciseId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCompletedLog);

        var dto = new RequestWorkoutCompletionDto
        {
            TrainingPlanExerciseId = exercise.TrainingPlanExerciseId,
            MediaUrl = "https://res.cloudinary.com/demo/image/upload/proof.jpg"
        };

        // Act & Assert
        await Assert.ThrowsAsync<BusinessException>(() => _service.RequestWorkoutCompletionAsync(traineeId, dto));
    }

    [Fact]
    public async Task UTCID03_RequestWorkoutCompletion_UnauthorizedTrainee_ThrowsForbiddenException()
    {
        // Arrange
        var actualBuyerId = Guid.NewGuid();
        var attackerTraineeId = Guid.NewGuid();
        var (plan, exercise) = CreateExerciseHierarchy(actualBuyerId);

        _mockPlanRepo.Setup(r => r.GetExerciseByIdAsync(exercise.TrainingPlanExerciseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exercise);

        var dto = new RequestWorkoutCompletionDto
        {
            TrainingPlanExerciseId = exercise.TrainingPlanExerciseId,
            MediaUrl = "https://example.com/proof.jpg"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() => _service.RequestWorkoutCompletionAsync(attackerTraineeId, dto));
    }

    [Fact]
    public async Task UTCID04_RequestWorkoutCompletion_NotesExceeds300Chars_ThrowsValidationException()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var dto = new RequestWorkoutCompletionDto
        {
            TrainingPlanExerciseId = Guid.NewGuid(),
            Notes = new string('A', 301)
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _service.RequestWorkoutCompletionAsync(traineeId, dto));
    }

    [Fact]
    public async Task UTCID05_RequestWorkoutCompletion_MediaUrlExceeds500Chars_ThrowsValidationException()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var dto = new RequestWorkoutCompletionDto
        {
            TrainingPlanExerciseId = Guid.NewGuid(),
            MediaUrl = "https://example.com/" + new string('b', 500)
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _service.RequestWorkoutCompletionAsync(traineeId, dto));
    }

    [Fact]
    public async Task UTCID12_RequestWorkoutCompletion_ResubmitRejectedLog_UpdatesExistingLogAndPreservesNotesWhenNull()
    {
        // Normal: Resubmitting after UNCOMPLETE updates existing log instead of adding a new row
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var (plan, exercise) = CreateExerciseHierarchy(traineeId, coachId);
        var existingLog = new WorkoutCompletionLog
        {
            WorkoutCompletionLogId = Guid.NewGuid(),
            TrainingPlanExerciseId = exercise.TrainingPlanExerciseId,
            TraineeId = traineeId,
            Status = "UNCOMPLETE",
            Notes = "Initial notes"
        };

        _mockPlanRepo.Setup(r => r.GetExerciseByIdAsync(exercise.TrainingPlanExerciseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exercise);
        _mockPlanRepo.Setup(r => r.GetWorkoutLogAsync(exercise.TrainingPlanExerciseId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLog);
        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(plan.OrderDetailsId!.Value, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var dto = new RequestWorkoutCompletionDto
        {
            TrainingPlanExerciseId = exercise.TrainingPlanExerciseId,
            MediaUrl = "  https://cdn.fitsocial.test/fixed_form.mp4  ",
            Notes = null
        };

        var result = await _service.RequestWorkoutCompletionAsync(traineeId, dto);

        Assert.Equal("PENDING_REVIEW", result.Status);
        Assert.Equal(existingLog.WorkoutCompletionLogId, result.WorkoutCompletionLogId);
        Assert.Equal("PENDING_REVIEW", existingLog.Status);
        Assert.Equal("https://cdn.fitsocial.test/fixed_form.mp4", existingLog.MediaUrl);
        Assert.Equal("Initial notes", existingLog.Notes);
        _mockPlanRepo.Verify(r => r.AddWorkoutLogAsync(It.IsAny<WorkoutCompletionLog>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task UTCID13_RequestWorkoutCompletion_ScheduledForBusinessToday_Succeeds()
    {
        // Normal: Exercise scheduled for GetBusinessToday() passes date gate and sets LogDate to scheduledDate
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var today = TrainingPlanService.GetBusinessToday();
        short todayDay = today.DayOfWeek == DayOfWeek.Sunday ? (short)7 : (short)today.DayOfWeek;
        var (plan, exercise) = CreateExerciseHierarchy(
            traineeId, coachId,
            planStartDate: today,
            planEndDate: today.AddDays(30),
            weekNumber: 1,
            dayNumber: todayDay);

        _mockPlanRepo.Setup(r => r.GetExerciseByIdAsync(exercise.TrainingPlanExerciseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exercise);
        _mockPlanRepo.Setup(r => r.GetWorkoutLogAsync(exercise.TrainingPlanExerciseId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkoutCompletionLog?)null);
        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(plan.OrderDetailsId!.Value, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var dto = new RequestWorkoutCompletionDto
        {
            TrainingPlanExerciseId = exercise.TrainingPlanExerciseId,
            MediaUrl = "https://cdn.fitsocial.test/today.jpg"
        };

        var result = await _service.RequestWorkoutCompletionAsync(traineeId, dto);

        Assert.Equal("PENDING_REVIEW", result.Status);
        _mockPlanRepo.Verify(r => r.AddWorkoutLogAsync(It.Is<WorkoutCompletionLog>(l => l.LogDate.Date == today.Date), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID14_RequestWorkoutCompletion_ForwardsCancellationTokenAndTrimsNotes()
    {
        // Normal: Explicit CancellationToken forwarded across repo, UoW, and notification repo
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var (plan, exercise) = CreateExerciseHierarchy(traineeId, coachId);

        _mockPlanRepo.Setup(r => r.GetExerciseByIdAsync(exercise.TrainingPlanExerciseId, token)).ReturnsAsync(exercise);
        _mockPlanRepo.Setup(r => r.GetWorkoutLogAsync(exercise.TrainingPlanExerciseId, traineeId, token)).ReturnsAsync((WorkoutCompletionLog?)null);
        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(plan.OrderDetailsId!.Value, traineeId, token)).ReturnsAsync(plan);

        var dto = new RequestWorkoutCompletionDto
        {
            TrainingPlanExerciseId = exercise.TrainingPlanExerciseId,
            Notes = "   Clean reps!   "
        };

        var result = await _service.RequestWorkoutCompletionAsync(traineeId, dto, token);

        Assert.Equal("Clean reps!", result.Notes);
        _mockUow.Verify(u => u.SaveChangesAsync(token), Times.Exactly(2));
        _mockNotificationRepo.Verify(n => n.AddAsync(It.IsAny<Notification>(), token), Times.Once);
    }

    [Fact]
    public async Task UTCID15_RequestWorkoutCompletion_Notes300AndMediaUrl500ExactBoundary_Succeeds()
    {
        // Boundary: Exact maximum allowed Notes (300 chars) and MediaUrl (500 chars) succeed
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var (plan, exercise) = CreateExerciseHierarchy(traineeId, coachId);

        _mockPlanRepo.Setup(r => r.GetExerciseByIdAsync(exercise.TrainingPlanExerciseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exercise);
        _mockPlanRepo.Setup(r => r.GetWorkoutLogAsync(exercise.TrainingPlanExerciseId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkoutCompletionLog?)null);
        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(plan.OrderDetailsId!.Value, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var dto = new RequestWorkoutCompletionDto
        {
            TrainingPlanExerciseId = exercise.TrainingPlanExerciseId,
            Notes = new string('N', 300),
            MediaUrl = new string('U', 500)
        };

        var result = await _service.RequestWorkoutCompletionAsync(traineeId, dto);

        Assert.Equal("PENDING_REVIEW", result.Status);
        Assert.Equal(300, result.Notes!.Length);
        Assert.Equal(500, result.MediaUrl!.Length);
    }

    [Fact]
    public async Task UTCID16_RequestWorkoutCompletion_CoachIdEmptyAndRefreshedPlanNull_SkipsNotificationAndReturnsZeroProgress()
    {
        // Boundary: CoachId == Guid.Empty skips notification; refreshed plan == null returns 0% progress safely
        var traineeId = Guid.NewGuid();
        var (plan, exercise) = CreateExerciseHierarchy(traineeId, Guid.Empty);

        _mockPlanRepo.Setup(r => r.GetExerciseByIdAsync(exercise.TrainingPlanExerciseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exercise);
        _mockPlanRepo.Setup(r => r.GetWorkoutLogAsync(exercise.TrainingPlanExerciseId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkoutCompletionLog?)null);
        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(plan.OrderDetailsId!.Value, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainingPlan?)null);

        var result = await _service.RequestWorkoutCompletionAsync(traineeId, new RequestWorkoutCompletionDto
        {
            TrainingPlanExerciseId = exercise.TrainingPlanExerciseId
        });

        Assert.Equal(0, result.TotalExercisesCount);
        Assert.Equal(0, result.CompletedExercisesCount);
        Assert.Equal(0.0, result.ProgressPercentage);
        _mockNotificationRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UTCID17_RequestWorkoutCompletion_ScheduledForDifferentDate_ThrowsValidationException()
    {
        // Boundary: Exercise scheduled on a day other than businessToday throws ValidationException
        var traineeId = Guid.NewGuid();
        var today = TrainingPlanService.GetBusinessToday();
        short todayDay = today.DayOfWeek == DayOfWeek.Sunday ? (short)7 : (short)today.DayOfWeek;
        short otherDay = (short)(todayDay == 1 ? 2 : 1);
        var (_, exercise) = CreateExerciseHierarchy(
            traineeId,
            planStartDate: today,
            planEndDate: today.AddDays(30),
            weekNumber: 1,
            dayNumber: otherDay);

        _mockPlanRepo.Setup(r => r.GetExerciseByIdAsync(exercise.TrainingPlanExerciseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exercise);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.RequestWorkoutCompletionAsync(traineeId, new RequestWorkoutCompletionDto
            {
                TrainingPlanExerciseId = exercise.TrainingPlanExerciseId
            }));

        Assert.Contains("You can only complete exercises scheduled for today", ex.Message);
    }

    [Fact]
    public async Task UTCID18_RequestWorkoutCompletion_EmptyTraineeId_ThrowsForbiddenException()
    {
        // Abnormal: Guid.Empty traineeId -> ForbiddenException
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.RequestWorkoutCompletionAsync(Guid.Empty, new RequestWorkoutCompletionDto
            {
                TrainingPlanExerciseId = Guid.NewGuid()
            }));

        Assert.Equal("Invalid trainee identity.", ex.Message);
        _mockPlanRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UTCID19_RequestWorkoutCompletion_NullDtoOrEmptyExerciseId_ThrowsValidationException()
    {
        // Abnormal: Null dto or Guid.Empty TrainingPlanExerciseId -> ValidationException
        var traineeId = Guid.NewGuid();

        var exNull = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.RequestWorkoutCompletionAsync(traineeId, null!));
        var exEmpty = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.RequestWorkoutCompletionAsync(traineeId, new RequestWorkoutCompletionDto { TrainingPlanExerciseId = Guid.Empty }));

        Assert.Equal("TrainingPlanExerciseId is required.", exNull.Message);
        Assert.Equal("TrainingPlanExerciseId is required.", exEmpty.Message);
    }

    [Fact]
    public async Task UTCID20_RequestWorkoutCompletion_ExerciseNotFound_ThrowsNotFoundException()
    {
        // Abnormal: Missing exercise -> NotFoundException
        var traineeId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        _mockPlanRepo.Setup(r => r.GetExerciseByIdAsync(exerciseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainingPlanExercise?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.RequestWorkoutCompletionAsync(traineeId, new RequestWorkoutCompletionDto { TrainingPlanExerciseId = exerciseId }));

        Assert.Equal("Exercise not found.", ex.Message);
    }

    [Fact]
    public async Task UTCID21_RequestWorkoutCompletion_OrderRefundedOrPending_ThrowsForbiddenException()
    {
        // Abnormal: Order status REFUNDED or PENDING blocks workout submission
        var traineeId = Guid.NewGuid();
        var (_, refundedEx) = CreateExerciseHierarchy(traineeId, orderStatus: PaymentConstants.OrderStatusRefunded);
        var (_, pendingEx) = CreateExerciseHierarchy(traineeId, orderStatus: PaymentConstants.OrderStatusPending);

        _mockPlanRepo.Setup(r => r.GetExerciseByIdAsync(refundedEx.TrainingPlanExerciseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refundedEx);
        _mockPlanRepo.Setup(r => r.GetExerciseByIdAsync(pendingEx.TrainingPlanExerciseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingEx);

        var exRefunded = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.RequestWorkoutCompletionAsync(traineeId, new RequestWorkoutCompletionDto { TrainingPlanExerciseId = refundedEx.TrainingPlanExerciseId }));
        var exPending = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.RequestWorkoutCompletionAsync(traineeId, new RequestWorkoutCompletionDto { TrainingPlanExerciseId = pendingEx.TrainingPlanExerciseId }));

        Assert.Contains("refunded", exRefunded.Message);
        Assert.Contains("PENDING", exPending.Message);
    }

    [Fact]
    public async Task UTCID22_RequestWorkoutCompletion_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Abnormal: Pre-cancelled CancellationToken throws OperationCanceledException
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var exId = Guid.NewGuid();

        _mockPlanRepo.Setup(r => r.GetExerciseByIdAsync(exId, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.RequestWorkoutCompletionAsync(Guid.NewGuid(), new RequestWorkoutCompletionDto { TrainingPlanExerciseId = exId }, cts.Token));
    }

    #endregion

    #region Coach: ReviewWorkoutCompletionAsync Tests

    [Fact]
    public async Task UTCID06_ReviewWorkoutCompletion_Approve_SetsCompleted_SendsNotificationToTrainee()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var (plan, exercise) = CreateExerciseHierarchy(traineeId, coachId);

        var existingPendingLog = new WorkoutCompletionLog
        {
            WorkoutCompletionLogId = Guid.NewGuid(),
            TrainingPlanExerciseId = exercise.TrainingPlanExerciseId,
            TraineeId = traineeId,
            Status = "PENDING_REVIEW",
            MediaUrl = "https://res.cloudinary.com/demo/video/upload/proof.mp4",
            Notes = "Felt great!",
            CreatedAt = DateTime.UtcNow
        };
        exercise.WorkoutCompletionLogs.Add(existingPendingLog);

        _mockPlanRepo.Setup(r => r.GetExerciseByIdAsync(exercise.TrainingPlanExerciseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exercise);

        _mockPlanRepo.Setup(r => r.GetWorkoutLogAsync(exercise.TrainingPlanExerciseId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPendingLog);

        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(plan.OrderDetailsId.Value, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var dto = new CoachReviewWorkoutDto
        {
            TrainingPlanExerciseId = exercise.TrainingPlanExerciseId,
            TraineeId = traineeId,
            Decision = "COMPLETED",
            Feedback = "Great form and execution, keep it up!"
        };

        // Act
        var result = await _service.ReviewWorkoutCompletionAsync(coachId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsCompleted);
        Assert.Equal("COMPLETED", result.Status);
        Assert.Equal(dto.Feedback, result.CoachFeedback);
        Assert.NotNull(result.ReviewedAt);
        Assert.Equal(coachId, existingPendingLog.ReviewedBy);

        _mockNotificationRepo.Verify(n => n.AddAsync(It.Is<Notification>(notif =>
            notif.UserId == traineeId &&
            notif.ActorId == coachId &&
            notif.Type == "WORKOUT_APPROVED" &&
            notif.Description.Contains("Great form and execution")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID07_ReviewWorkoutCompletion_Reject_RequiresFeedback_SetsUncomplete_SendsNotificationToTrainee()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var (plan, exercise) = CreateExerciseHierarchy(traineeId, coachId);

        var existingPendingLog = new WorkoutCompletionLog
        {
            WorkoutCompletionLogId = Guid.NewGuid(),
            TrainingPlanExerciseId = exercise.TrainingPlanExerciseId,
            TraineeId = traineeId,
            Status = "PENDING_REVIEW",
            MediaUrl = "https://res.cloudinary.com/demo/video/upload/proof.mp4",
            CreatedAt = DateTime.UtcNow
        };
        exercise.WorkoutCompletionLogs.Add(existingPendingLog);

        _mockPlanRepo.Setup(r => r.GetExerciseByIdAsync(exercise.TrainingPlanExerciseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exercise);

        _mockPlanRepo.Setup(r => r.GetWorkoutLogAsync(exercise.TrainingPlanExerciseId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPendingLog);

        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(plan.OrderDetailsId.Value, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var dto = new CoachReviewWorkoutDto
        {
            TrainingPlanExerciseId = exercise.TrainingPlanExerciseId,
            TraineeId = traineeId,
            Decision = "UNCOMPLETE",
            Feedback = "Your back arched on the last 2 reps, please fix your form and resubmit."
        };

        // Act
        var result = await _service.ReviewWorkoutCompletionAsync(coachId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsCompleted);
        Assert.Equal("UNCOMPLETE", result.Status);
        Assert.Equal(dto.Feedback, result.CoachFeedback);
        Assert.Equal(coachId, existingPendingLog.ReviewedBy);

        _mockNotificationRepo.Verify(n => n.AddAsync(It.Is<Notification>(notif =>
            notif.UserId == traineeId &&
            notif.ActorId == coachId &&
            notif.Type == "WORKOUT_REJECTED" &&
            notif.Description.Contains("Your back arched")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID08_ReviewWorkoutCompletion_Reject_MissingFeedback_ThrowsValidationException()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var dto = new CoachReviewWorkoutDto
        {
            TrainingPlanExerciseId = Guid.NewGuid(),
            TraineeId = Guid.NewGuid(),
            Decision = "UNCOMPLETE",
            Feedback = "" // Empty feedback on rejection!
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.ReviewWorkoutCompletionAsync(coachId, dto));
        Assert.Contains("Feedback is required", ex.Message);
    }

    [Fact]
    public async Task UTCID09_ReviewWorkoutCompletion_UnauthorizedCoach_ThrowsForbiddenException()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var actualCoachId = Guid.NewGuid();
        var strangerCoachId = Guid.NewGuid();
        var (plan, exercise) = CreateExerciseHierarchy(traineeId, actualCoachId);

        _mockPlanRepo.Setup(r => r.GetExerciseByIdAsync(exercise.TrainingPlanExerciseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exercise);

        var dto = new CoachReviewWorkoutDto
        {
            TrainingPlanExerciseId = exercise.TrainingPlanExerciseId,
            TraineeId = traineeId,
            Decision = "COMPLETED",
            Feedback = "Great job!"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() => _service.ReviewWorkoutCompletionAsync(strangerCoachId, dto));
    }

    [Fact]
    public async Task UTCID10_ReviewWorkoutCompletion_InvalidDecision_ThrowsValidationException()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var dto = new CoachReviewWorkoutDto
        {
            TrainingPlanExerciseId = Guid.NewGuid(),
            TraineeId = Guid.NewGuid(),
            Decision = "INVALID_STATUS"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _service.ReviewWorkoutCompletionAsync(coachId, dto));
    }

    [Fact]
    public async Task UTCID11_ReviewWorkoutCompletion_100PercentDone_AutoCompletesTrainingPlan()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var (plan, exercise) = CreateExerciseHierarchy(traineeId, coachId);
        plan.Status = "IN_PROGRESS";

        var log = new WorkoutCompletionLog
        {
            WorkoutCompletionLogId = Guid.NewGuid(),
            TrainingPlanExerciseId = exercise.TrainingPlanExerciseId,
            TraineeId = traineeId,
            Status = "PENDING_REVIEW"
        };
        exercise.WorkoutCompletionLogs.Add(log);

        _mockPlanRepo.Setup(r => r.GetExerciseByIdAsync(exercise.TrainingPlanExerciseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exercise);

        _mockPlanRepo.Setup(r => r.GetWorkoutLogAsync(exercise.TrainingPlanExerciseId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(log);

        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(plan.OrderDetailsId.Value, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var dto = new CoachReviewWorkoutDto
        {
            TrainingPlanExerciseId = exercise.TrainingPlanExerciseId,
            TraineeId = traineeId,
            Decision = "COMPLETED",
            Feedback = "Congratulations on finishing the final exercise of the program!"
        };

        // Act
        var result = await _service.ReviewWorkoutCompletionAsync(coachId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsCompleted);
        Assert.Equal("COMPLETED", plan.Status); // TrainingPlan automatically set to COMPLETED!
    }

    [Fact]
    public async Task UTCID23_ReviewWorkoutCompletion_WhenNoExistingLogOrIncomplete_ThrowsBusinessException()
    {
        // Abnormal: Coach cannot review when trainee has not submitted a completion request (existingLog == null or INCOMPLETE)
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var (plan, exercise) = CreateExerciseHierarchy(traineeId, coachId);

        _mockPlanRepo.Setup(r => r.GetExerciseByIdAsync(exercise.TrainingPlanExerciseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exercise);
        _mockPlanRepo.Setup(r => r.GetWorkoutLogAsync(exercise.TrainingPlanExerciseId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkoutCompletionLog?)null);

        var dto = new CoachReviewWorkoutDto
        {
            TrainingPlanExerciseId = exercise.TrainingPlanExerciseId,
            TraineeId = traineeId,
            Decision = "COMPLETED",
            Feedback = "Approved"
        };

        var exNull = await Assert.ThrowsAsync<BusinessException>(() => _service.ReviewWorkoutCompletionAsync(coachId, dto));
        Assert.Equal("Trainee has not submitted a completion request for this exercise yet.", exNull.Message);

        _mockPlanRepo.Setup(r => r.GetWorkoutLogAsync(exercise.TrainingPlanExerciseId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkoutCompletionLog
            {
                WorkoutCompletionLogId = Guid.NewGuid(),
                TraineeId = traineeId,
                TrainingPlanExerciseId = exercise.TrainingPlanExerciseId,
                Status = "INCOMPLETE"
            });

        var exIncomplete = await Assert.ThrowsAsync<BusinessException>(() => _service.ReviewWorkoutCompletionAsync(coachId, dto));
        Assert.Equal("Trainee has not submitted a completion request for this exercise yet.", exIncomplete.Message);

        _mockPlanRepo.Verify(r => r.AddWorkoutLogAsync(It.IsAny<WorkoutCompletionLog>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UTCID24_ReviewWorkoutCompletion_ApproveWithoutFeedback_SendsDefaultMessageAndForwardsToken()
    {
        // Normal: Case-insensitive " completed " with Feedback=null sends approval notification without feedback suffix
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var (plan, exercise) = CreateExerciseHierarchy(traineeId, coachId);
        var log = new WorkoutCompletionLog { WorkoutCompletionLogId = Guid.NewGuid(), TraineeId = traineeId, Status = "PENDING_REVIEW" };

        _mockPlanRepo.Setup(r => r.GetExerciseByIdAsync(exercise.TrainingPlanExerciseId, token)).ReturnsAsync(exercise);
        _mockPlanRepo.Setup(r => r.GetWorkoutLogAsync(exercise.TrainingPlanExerciseId, traineeId, token)).ReturnsAsync(log);
        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(plan.OrderDetailsId!.Value, traineeId, token)).ReturnsAsync(plan);

        var result = await _service.ReviewWorkoutCompletionAsync(coachId, new CoachReviewWorkoutDto
        {
            TrainingPlanExerciseId = exercise.TrainingPlanExerciseId,
            TraineeId = traineeId,
            Decision = "  completed  ",
            Feedback = null
        }, token);

        Assert.True(result.IsCompleted);
        Assert.Null(result.CoachFeedback);
        _mockNotificationRepo.Verify(n => n.AddAsync(It.Is<Notification>(notif =>
            notif.Type == "WORKOUT_APPROVED" &&
            !notif.Description!.Contains("Feedback:")), token), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(token), Times.AtLeastOnce);
    }

    [Fact]
    public async Task UTCID25_ReviewWorkoutCompletion_PartialPlanCompletion_KeepsPlanInProgress()
    {
        // Boundary: 1 of 2 exercises COMPLETED (50%) -> TrainingPlan.Status remains IN_PROGRESS
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var (plan, exercise1) = CreateExerciseHierarchy(traineeId, coachId);
        var exercise2 = new TrainingPlanExercise
        {
            TrainingPlanExerciseId = Guid.NewGuid(),
            TrainingPlanId = plan.TrainingPlanId,
            ExerciseName = "Incline Press",
            WorkoutCompletionLogs = new List<WorkoutCompletionLog>()
        };
        plan.TrainingPlanExercises.Add(exercise2);

        var log1 = new WorkoutCompletionLog { WorkoutCompletionLogId = Guid.NewGuid(), TraineeId = traineeId, Status = "PENDING_REVIEW" };
        exercise1.WorkoutCompletionLogs.Add(log1);

        _mockPlanRepo.Setup(r => r.GetExerciseByIdAsync(exercise1.TrainingPlanExerciseId, It.IsAny<CancellationToken>())).ReturnsAsync(exercise1);
        _mockPlanRepo.Setup(r => r.GetWorkoutLogAsync(exercise1.TrainingPlanExerciseId, traineeId, It.IsAny<CancellationToken>())).ReturnsAsync(log1);
        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(plan.OrderDetailsId!.Value, traineeId, It.IsAny<CancellationToken>())).ReturnsAsync(plan);

        var result = await _service.ReviewWorkoutCompletionAsync(coachId, new CoachReviewWorkoutDto
        {
            TrainingPlanExerciseId = exercise1.TrainingPlanExerciseId,
            TraineeId = traineeId,
            Decision = "COMPLETED"
        });

        Assert.Equal(2, result.TotalExercisesCount);
        Assert.Equal(1, result.CompletedExercisesCount);
        Assert.Equal(50.0, result.ProgressPercentage);
        Assert.Equal("IN_PROGRESS", plan.Status);
    }

    [Fact]
    public async Task UTCID26_ReviewWorkoutCompletion_FeedbackExactly500Chars_Succeeds()
    {
        // Boundary: Exact maximum 500-char Feedback succeeds
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var (plan, exercise) = CreateExerciseHierarchy(traineeId, coachId);
        var log = new WorkoutCompletionLog { WorkoutCompletionLogId = Guid.NewGuid(), TraineeId = traineeId, Status = "PENDING_REVIEW" };

        _mockPlanRepo.Setup(r => r.GetExerciseByIdAsync(exercise.TrainingPlanExerciseId, It.IsAny<CancellationToken>())).ReturnsAsync(exercise);
        _mockPlanRepo.Setup(r => r.GetWorkoutLogAsync(exercise.TrainingPlanExerciseId, traineeId, It.IsAny<CancellationToken>())).ReturnsAsync(log);
        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(plan.OrderDetailsId!.Value, traineeId, It.IsAny<CancellationToken>())).ReturnsAsync(plan);

        var maxFeedback = new string('F', 500);
        var result = await _service.ReviewWorkoutCompletionAsync(coachId, new CoachReviewWorkoutDto
        {
            TrainingPlanExerciseId = exercise.TrainingPlanExerciseId,
            TraineeId = traineeId,
            Decision = "UNCOMPLETE",
            Feedback = maxFeedback
        });

        Assert.Equal("UNCOMPLETE", result.Status);
        Assert.Equal(500, result.CoachFeedback!.Length);
    }

    [Fact]
    public async Task UTCID27_ReviewWorkoutCompletion_FeedbackExceeds500Chars_ThrowsValidationException()
    {
        // Boundary: 501-char Feedback (just over max 500) -> ValidationException
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ReviewWorkoutCompletionAsync(Guid.NewGuid(), new CoachReviewWorkoutDto
            {
                TrainingPlanExerciseId = Guid.NewGuid(),
                TraineeId = Guid.NewGuid(),
                Decision = "COMPLETED",
                Feedback = new string('F', 501)
            }));

        Assert.Equal("Feedback cannot exceed 500 characters.", ex.Message);
    }

    [Fact]
    public async Task UTCID28_ReviewWorkoutCompletion_EmptyCoachId_ThrowsForbiddenException()
    {
        // Abnormal: Guid.Empty coachId -> ForbiddenException
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.ReviewWorkoutCompletionAsync(Guid.Empty, new CoachReviewWorkoutDto
            {
                TrainingPlanExerciseId = Guid.NewGuid(),
                TraineeId = Guid.NewGuid(),
                Decision = "COMPLETED"
            }));

        Assert.Equal("Invalid coach identity.", ex.Message);
    }

    [Fact]
    public async Task UTCID29_ReviewWorkoutCompletion_NullDtoOrEmptyIds_ThrowsValidationException()
    {
        // Abnormal: Null dto, Guid.Empty ExerciseId, or Guid.Empty TraineeId -> ValidationException
        var coachId = Guid.NewGuid();

        var exNull = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ReviewWorkoutCompletionAsync(coachId, null!));
        var exEx = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ReviewWorkoutCompletionAsync(coachId, new CoachReviewWorkoutDto
            {
                TrainingPlanExerciseId = Guid.Empty,
                TraineeId = Guid.NewGuid(),
                Decision = "COMPLETED"
            }));
        var exTrainee = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ReviewWorkoutCompletionAsync(coachId, new CoachReviewWorkoutDto
            {
                TrainingPlanExerciseId = Guid.NewGuid(),
                TraineeId = Guid.Empty,
                Decision = "COMPLETED"
            }));

        Assert.Equal("TrainingPlanExerciseId is required.", exNull.Message);
        Assert.Equal("TrainingPlanExerciseId is required.", exEx.Message);
        Assert.Equal("TraineeId is required.", exTrainee.Message);
    }

    [Fact]
    public async Task UTCID30_ReviewWorkoutCompletion_ExerciseNotFound_ThrowsNotFoundException()
    {
        // Abnormal: Missing exercise -> NotFoundException
        var exerciseId = Guid.NewGuid();
        _mockPlanRepo.Setup(r => r.GetExerciseByIdAsync(exerciseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainingPlanExercise?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ReviewWorkoutCompletionAsync(Guid.NewGuid(), new CoachReviewWorkoutDto
            {
                TrainingPlanExerciseId = exerciseId,
                TraineeId = Guid.NewGuid(),
                Decision = "COMPLETED"
            }));

        Assert.Equal("Exercise not found.", ex.Message);
    }

    [Fact]
    public async Task UTCID31_ReviewWorkoutCompletion_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Abnormal: Pre-cancelled CancellationToken throws OperationCanceledException
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var exId = Guid.NewGuid();

        _mockPlanRepo.Setup(r => r.GetExerciseByIdAsync(exId, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.ReviewWorkoutCompletionAsync(Guid.NewGuid(), new CoachReviewWorkoutDto
            {
                TrainingPlanExerciseId = exId,
                TraineeId = Guid.NewGuid(),
                Decision = "COMPLETED"
            }, cts.Token));
    }

    #endregion

    #region TraineeWorkoutController.RequestWorkoutCompletion (8 Tests)

    private static TraineeWorkoutController CreateTraineeController(Mock<ITrainingPlanService> mockService, params Claim[] claims)
    {
        var controller = new TraineeWorkoutController(mockService.Object, new Mock<ILogger<TraineeWorkoutController>>().Object);
        var identity = claims.Length > 0 ? new ClaimsIdentity(claims, "TestAuth") : new ClaimsIdentity();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    [Fact]
    public async Task TraineeController_RequestWorkoutCompletion_WithNameIdentifier_Returns200Ok()
    {
        // CtrlRequest UTCID01 - Normal: Valid NameIdentifier submits request -> 200 OK
        var mockService = new Mock<ITrainingPlanService>();
        var traineeId = Guid.NewGuid();
        var controller = CreateTraineeController(mockService, new Claim(ClaimTypes.NameIdentifier, traineeId.ToString()));

        var dto = new RequestWorkoutCompletionDto { TrainingPlanExerciseId = Guid.NewGuid(), MediaUrl = "https://cdn/v.mp4" };
        var expected = new WorkoutCompletionResultDto { TrainingPlanExerciseId = dto.TrainingPlanExerciseId, Status = "PENDING_REVIEW" };
        mockService.Setup(s => s.RequestWorkoutCompletionAsync(traineeId, dto, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await controller.RequestWorkoutCompletion(dto, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponseDto<WorkoutCompletionResultDto>>(ok.Value);
        Assert.True(body.Success);
        Assert.Equal("Workout completion request submitted successfully. Waiting for coach review.", body.Message);
        Assert.Equal("PENDING_REVIEW", body.Data!.Status);
    }

    [Fact]
    public async Task TraineeController_RequestWorkoutCompletion_WithSubClaimFallback_Returns200Ok()
    {
        // CtrlRequest UTCID02 - Normal: Falls back to "sub" claim -> 200 OK
        var mockService = new Mock<ITrainingPlanService>();
        var traineeId = Guid.NewGuid();
        var controller = CreateTraineeController(mockService, new Claim("sub", traineeId.ToString()));

        var dto = new RequestWorkoutCompletionDto { TrainingPlanExerciseId = Guid.NewGuid() };
        mockService.Setup(s => s.RequestWorkoutCompletionAsync(traineeId, dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkoutCompletionResultDto { Status = "PENDING_REVIEW" });

        var result = await controller.RequestWorkoutCompletion(dto, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        mockService.Verify(s => s.RequestWorkoutCompletionAsync(traineeId, dto, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TraineeController_RequestWorkoutCompletion_ForwardsCancellationToken()
    {
        // CtrlRequest UTCID03 - Normal: Explicit CancellationToken forwarded to service
        var mockService = new Mock<ITrainingPlanService>();
        var traineeId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var controller = CreateTraineeController(mockService, new Claim(ClaimTypes.NameIdentifier, traineeId.ToString()));

        var dto = new RequestWorkoutCompletionDto { TrainingPlanExerciseId = Guid.NewGuid() };
        mockService.Setup(s => s.RequestWorkoutCompletionAsync(traineeId, dto, token))
            .ReturnsAsync(new WorkoutCompletionResultDto { Status = "PENDING_REVIEW" });

        var result = await controller.RequestWorkoutCompletion(dto, token);

        Assert.IsType<OkObjectResult>(result);
        mockService.Verify(s => s.RequestWorkoutCompletionAsync(traineeId, dto, token), Times.Once);
    }

    [Fact]
    public async Task TraineeController_RequestWorkoutCompletion_EmptyOrWhitespaceClaim_Returns401Unauthorized()
    {
        // CtrlRequest UTCID04 - Boundary: Whitespace NameIdentifier -> 401 Unauthorized
        var mockService = new Mock<ITrainingPlanService>();
        var controller = CreateTraineeController(mockService, new Claim(ClaimTypes.NameIdentifier, "   "));

        var result = await controller.RequestWorkoutCompletion(new RequestWorkoutCompletionDto(), CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var body = Assert.IsType<ApiResponseDto<WorkoutCompletionResultDto>>(unauthorized.Value);
        Assert.False(body.Success);
        Assert.Equal("Unauthorized: Invalid user identity.", body.Message);
        mockService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TraineeController_RequestWorkoutCompletion_PartialProgressResult_Returns200WithMetrics()
    {
        // CtrlRequest UTCID05 - Boundary: 50% plan progress returned cleanly in 200 OK
        var mockService = new Mock<ITrainingPlanService>();
        var traineeId = Guid.NewGuid();
        var controller = CreateTraineeController(mockService, new Claim(ClaimTypes.NameIdentifier, traineeId.ToString()));

        var dto = new RequestWorkoutCompletionDto { TrainingPlanExerciseId = Guid.NewGuid() };
        mockService.Setup(s => s.RequestWorkoutCompletionAsync(traineeId, dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkoutCompletionResultDto { Status = "PENDING_REVIEW", TotalExercisesCount = 2, CompletedExercisesCount = 1, ProgressPercentage = 50.0 });

        var result = await controller.RequestWorkoutCompletion(dto, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponseDto<WorkoutCompletionResultDto>>(ok.Value);
        Assert.Equal(50.0, body.Data!.ProgressPercentage);
    }

    [Fact]
    public async Task TraineeController_RequestWorkoutCompletion_Unauthenticated_Returns401Unauthorized()
    {
        // CtrlRequest UTCID06 - Abnormal: Missing user claims -> 401 Unauthorized
        var mockService = new Mock<ITrainingPlanService>();
        var controller = CreateTraineeController(mockService);

        var result = await controller.RequestWorkoutCompletion(new RequestWorkoutCompletionDto(), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        mockService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TraineeController_RequestWorkoutCompletion_InvalidGuidClaim_Returns401Unauthorized()
    {
        // CtrlRequest UTCID07 - Abnormal: Non-GUID claim -> 401 Unauthorized
        var mockService = new Mock<ITrainingPlanService>();
        var controller = CreateTraineeController(mockService, new Claim(ClaimTypes.NameIdentifier, "bad-guid"));

        var result = await controller.RequestWorkoutCompletion(new RequestWorkoutCompletionDto(), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        mockService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TraineeController_RequestWorkoutCompletion_ServiceThrowsBusinessException_PropagatesException()
    {
        // CtrlRequest UTCID08 - Abnormal: BusinessException from service is logged and rethrown
        var mockService = new Mock<ITrainingPlanService>();
        var traineeId = Guid.NewGuid();
        var controller = CreateTraineeController(mockService, new Claim(ClaimTypes.NameIdentifier, traineeId.ToString()));

        var dto = new RequestWorkoutCompletionDto { TrainingPlanExerciseId = Guid.NewGuid() };
        mockService.Setup(s => s.RequestWorkoutCompletionAsync(traineeId, dto, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessException("This exercise has already been completed and approved by coach."));

        await Assert.ThrowsAsync<BusinessException>(() => controller.RequestWorkoutCompletion(dto, CancellationToken.None));
    }

    #endregion

    #region CoachTrainingPlansController.ReviewWorkout (8 Tests)

    private static CoachTrainingPlansController CreateCoachController(Mock<ITrainingPlanService> mockService, params Claim[] claims)
    {
        var controller = new CoachTrainingPlansController(
            mockService.Object,
            new Mock<ITransactionService>().Object,
            new Mock<ILogger<CoachTrainingPlansController>>().Object);
        var identity = claims.Length > 0 ? new ClaimsIdentity(claims, "TestAuth") : new ClaimsIdentity();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    [Fact]
    public async Task CoachController_ReviewWorkout_WithNameIdentifier_Returns200Ok()
    {
        // CtrlReview UTCID01 - Normal: Coach approves workout -> 200 OK
        var mockService = new Mock<ITrainingPlanService>();
        var coachId = Guid.NewGuid();
        var controller = CreateCoachController(mockService, new Claim(ClaimTypes.NameIdentifier, coachId.ToString()));

        var dto = new CoachReviewWorkoutDto { TrainingPlanExerciseId = Guid.NewGuid(), TraineeId = Guid.NewGuid(), Decision = "COMPLETED" };
        mockService.Setup(s => s.ReviewWorkoutCompletionAsync(coachId, dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkoutCompletionResultDto { Status = "COMPLETED", IsCompleted = true });

        var result = await controller.ReviewWorkout(dto, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponseDto<WorkoutCompletionResultDto>>(ok.Value);
        Assert.True(body.Success);
        Assert.Equal("Workout completion reviewed successfully.", body.Message);
        Assert.True(body.Data!.IsCompleted);
    }

    [Fact]
    public async Task CoachController_ReviewWorkout_WithSubClaimFallback_Returns200Ok()
    {
        // CtrlReview UTCID02 - Normal: Falls back to "sub" claim -> 200 OK
        var mockService = new Mock<ITrainingPlanService>();
        var coachId = Guid.NewGuid();
        var controller = CreateCoachController(mockService, new Claim("sub", coachId.ToString()));

        var dto = new CoachReviewWorkoutDto { TrainingPlanExerciseId = Guid.NewGuid(), TraineeId = Guid.NewGuid(), Decision = "UNCOMPLETE", Feedback = "Fix depth" };
        mockService.Setup(s => s.ReviewWorkoutCompletionAsync(coachId, dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkoutCompletionResultDto { Status = "UNCOMPLETE", IsCompleted = false });

        var result = await controller.ReviewWorkout(dto, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        mockService.Verify(s => s.ReviewWorkoutCompletionAsync(coachId, dto, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CoachController_ReviewWorkout_ForwardsCancellationToken()
    {
        // CtrlReview UTCID03 - Normal: Explicit CancellationToken forwarded to service
        var mockService = new Mock<ITrainingPlanService>();
        var coachId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var controller = CreateCoachController(mockService, new Claim(ClaimTypes.NameIdentifier, coachId.ToString()));

        var dto = new CoachReviewWorkoutDto { TrainingPlanExerciseId = Guid.NewGuid(), TraineeId = Guid.NewGuid(), Decision = "COMPLETED" };
        mockService.Setup(s => s.ReviewWorkoutCompletionAsync(coachId, dto, token))
            .ReturnsAsync(new WorkoutCompletionResultDto { Status = "COMPLETED" });

        var result = await controller.ReviewWorkout(dto, token);

        Assert.IsType<OkObjectResult>(result);
        mockService.Verify(s => s.ReviewWorkoutCompletionAsync(coachId, dto, token), Times.Once);
    }

    [Fact]
    public async Task CoachController_ReviewWorkout_WhitespaceClaim_Returns401Unauthorized()
    {
        // CtrlReview UTCID04 - Boundary: Whitespace NameIdentifier -> 401 Unauthorized
        var mockService = new Mock<ITrainingPlanService>();
        var controller = CreateCoachController(mockService, new Claim(ClaimTypes.NameIdentifier, "   "));

        var result = await controller.ReviewWorkout(new CoachReviewWorkoutDto(), CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var body = Assert.IsType<ApiResponseDto<WorkoutCompletionResultDto>>(unauthorized.Value);
        Assert.False(body.Success);
        Assert.Equal("Unauthorized: Invalid coach identity in token.", body.Message);
        mockService.VerifyNoOtherCalls();
    }

    [Fact]
    public void CoachController_HasAuthorizeRoleCoachAttribute()
    {
        // CtrlReview UTCID05 - Boundary: Controller requires COACH role authorization
        var attr = typeof(CoachTrainingPlansController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();
        Assert.NotNull(attr);
        Assert.Equal("COACH", attr!.Roles);
    }

    [Fact]
    public async Task CoachController_ReviewWorkout_Unauthenticated_Returns401Unauthorized()
    {
        // CtrlReview UTCID06 - Abnormal: Missing user claims -> 401 Unauthorized
        var mockService = new Mock<ITrainingPlanService>();
        var controller = CreateCoachController(mockService);

        var result = await controller.ReviewWorkout(new CoachReviewWorkoutDto(), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        mockService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CoachController_ReviewWorkout_InvalidGuidClaim_Returns401Unauthorized()
    {
        // CtrlReview UTCID07 - Abnormal: Invalid non-GUID claim -> 401 Unauthorized
        var mockService = new Mock<ITrainingPlanService>();
        var controller = CreateCoachController(mockService, new Claim("sub", "invalid-guid"));

        var result = await controller.ReviewWorkout(new CoachReviewWorkoutDto(), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        mockService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CoachController_ReviewWorkout_ServiceThrowsForbiddenException_PropagatesException()
    {
        // CtrlReview UTCID08 - Abnormal: ForbiddenException from service is logged and rethrown
        var mockService = new Mock<ITrainingPlanService>();
        var coachId = Guid.NewGuid();
        var controller = CreateCoachController(mockService, new Claim(ClaimTypes.NameIdentifier, coachId.ToString()));

        var dto = new CoachReviewWorkoutDto { TrainingPlanExerciseId = Guid.NewGuid(), TraineeId = Guid.NewGuid(), Decision = "COMPLETED" };
        mockService.Setup(s => s.ReviewWorkoutCompletionAsync(coachId, dto, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ForbiddenException("You are not authorized to review exercises for this training plan."));

        await Assert.ThrowsAsync<ForbiddenException>(() => controller.ReviewWorkout(dto, CancellationToken.None));
    }

    #endregion

    #region TrainingPlanRepository.GetExerciseByIdAsync (5 Tests)

    private static FitSocialDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<FitSocialDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new FitSocialDbContext(options);
    }

    [Fact]
    public async Task Repository_GetExerciseByIdAsync_WithFullHierarchy_LoadsTrainingPlanOrderBuyerAndPackage()
    {
        // Repo UTCID01 - Normal: Loads TrainingPlan, OrderDetails, Order, Buyer, and Package
        using var context = CreateInMemoryDbContext();
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var pkgId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var odId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();

        context.Users.Add(new User { UserId = traineeId, Email = "t@test.com", FullName = "Trainee One" });
        context.TrainingPackages.Add(new TrainingPackage { PackageId = pkgId, CoachId = coachId, Title = "Strength Pro" });
        context.Orders.Add(new Order { OrderId = orderId, BuyerId = traineeId, CoachId = coachId, OrderStatus = "PAID" });
        context.OrderDetails.Add(new OrderDetail { OrderDetailsId = odId, OrderId = orderId, PackageId = pkgId, PackageTitle = "Strength Pro" });
        context.TrainingPlans.Add(new TrainingPlan { TrainingPlanId = planId, CoachId = coachId, OrderDetailsId = odId, Title = "Plan A" });
        context.TrainingPlanExercises.Add(new TrainingPlanExercise { TrainingPlanExerciseId = exerciseId, TrainingPlanId = planId, ExerciseName = "Squat" });
        await context.SaveChangesAsync();

        var repo = new TrainingPlanRepository(context);
        var loaded = await repo.GetExerciseByIdAsync(exerciseId);

        Assert.NotNull(loaded);
        Assert.Equal("Squat", loaded!.ExerciseName);
        Assert.Equal("Trainee One", loaded.TrainingPlan.OrderDetails!.Order.Buyer.FullName);
        Assert.Equal("Strength Pro", loaded.TrainingPlan.OrderDetails.Package!.Title);
    }

    [Fact]
    public async Task Repository_GetExerciseByIdAsync_WhenPackageIsNull_StillLoadsExerciseAndOrder()
    {
        // Repo UTCID02 - Normal: Exercise whose OrderDetails.PackageId is null loads cleanly
        using var context = CreateInMemoryDbContext();
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var odId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();

        context.Users.Add(new User { UserId = traineeId, Email = "t2@test.com", FullName = "Trainee Two" });
        context.Orders.Add(new Order { OrderId = orderId, BuyerId = traineeId, CoachId = coachId, OrderStatus = "PAID" });
        context.OrderDetails.Add(new OrderDetail { OrderDetailsId = odId, OrderId = orderId, PackageId = null });
        context.TrainingPlans.Add(new TrainingPlan { TrainingPlanId = planId, CoachId = coachId, OrderDetailsId = odId });
        context.TrainingPlanExercises.Add(new TrainingPlanExercise { TrainingPlanExerciseId = exerciseId, TrainingPlanId = planId, ExerciseName = "Plank" });
        await context.SaveChangesAsync();

        var repo = new TrainingPlanRepository(context);
        var loaded = await repo.GetExerciseByIdAsync(exerciseId);

        Assert.NotNull(loaded);
        Assert.Equal(traineeId, loaded!.TrainingPlan.OrderDetails!.Order.BuyerId);
        Assert.Null(loaded.TrainingPlan.OrderDetails.Package);
    }

    [Fact]
    public async Task Repository_GetExerciseByIdAsync_WhenNotFound_ReturnsNull()
    {
        // Repo UTCID03 - Boundary: Unknown exerciseId returns null
        using var context = CreateInMemoryDbContext();
        var repo = new TrainingPlanRepository(context);

        Assert.Null(await repo.GetExerciseByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Repository_GetExerciseByIdAsync_WhenEmptyGuid_ReturnsNull()
    {
        // Repo UTCID04 - Boundary: Guid.Empty returns null
        using var context = CreateInMemoryDbContext();
        var repo = new TrainingPlanRepository(context);

        Assert.Null(await repo.GetExerciseByIdAsync(Guid.Empty));
    }

    [Fact]
    public async Task Repository_GetExerciseByIdAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Repo UTCID05 - Abnormal: Cancelled token throws OperationCanceledException
        using var context = CreateInMemoryDbContext();
        var repo = new TrainingPlanRepository(context);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            repo.GetExerciseByIdAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion
}
