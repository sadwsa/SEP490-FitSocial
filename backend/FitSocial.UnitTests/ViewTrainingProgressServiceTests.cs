using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.TrainingPlan;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

/// <summary>
/// Unit test suite for View Training Progress feature:
/// TrainingPlanService.GetTraineeWorkoutPlanAsync and GetTraineeWorkoutPlanByPackageAsync
/// Covers:
/// - Normal test cases (UTCID01 - UTCID10): Active plans, backfill incomplete logs, pending coach placeholder, draft status, adherence rate, allowed order statuses
/// - Boundary test cases (UTCID11 - UTCID16): 0 exercises, no start date, fallback package/coach titles, Guid.Empty boundaries
/// - Abnormal test cases (UTCID17 - UTCID22): Not found order detail, unauthorized trainee, refunded package, disallowed status, missing plan by package, cancellation token
/// </summary>
public class ViewTrainingProgressServiceTests
{
    private readonly Mock<ITrainingPlanRepository> _mockPlanRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<INotificationRepository> _mockNotificationRepo;
    private readonly TrainingPlanService _service;

    public ViewTrainingProgressServiceTests()
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

    private static (TrainingPlan Plan, OrderDetail OrderDetail) CreateFullPlanHierarchy(
        Guid traineeId,
        Guid? coachId = null,
        DateTime? planStartDate = null,
        DateTime? planEndDate = null,
        string orderStatus = "PAID",
        string planStatus = "IN_PROGRESS",
        List<TrainingPlanExercise>? exercises = null)
    {
        var cId = coachId ?? Guid.NewGuid();
        var orderDetailsId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var packageId = Guid.NewGuid();

        var buyer = new User
        {
            UserId = traineeId,
            FullName = "Trainee Nguyen",
            Email = "trainee@fitsocial.com"
        };

        var coachUser = new User
        {
            UserId = cId,
            FullName = "Coach Le Van",
            AvatarUrl = "https://cdn.fitsocial.com/coach.jpg"
        };

        var coachProfile = new CoachProfile
        {
            CoachId = cId,
            Coach = coachUser
        };

        var order = new Order
        {
            OrderId = orderId,
            BuyerId = traineeId,
            Buyer = buyer,
            CoachId = cId,
            OrderStatus = orderStatus
        };

        var package = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = cId,
            Title = "Elite Body Transformation 60 Days",
            PackageType = "BOTH",
            Coach = coachProfile
        };

        var orderDetail = new OrderDetail
        {
            OrderDetailsId = orderDetailsId,
            OrderId = orderId,
            Order = order,
            PackageId = packageId,
            Package = package,
            PackageTitle = "Elite Body Transformation 60 Days",
            PackageType = "BOTH",
            CoachName = "Coach Le Van"
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
            Title = "60-Day Hypertrophy Plan",
            Description = "Customized hypertrophy schedule",
            TrainingPlanExercises = exercises ?? new List<TrainingPlanExercise>()
        };

        return (plan, orderDetail);
    }

    #endregion

    #region Normal Test Cases (UTCID01 - UTCID10)

    [Fact]
    public async Task UTCID01_GetTraineeWorkoutPlanAsync_ActivePlanWithExercises_ReturnsFullProgressSuccessfully()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var businessToday = TrainingPlanService.GetBusinessToday();
        var (plan, orderDetail) = CreateFullPlanHierarchy(
            traineeId, planStartDate: businessToday, planEndDate: businessToday.AddDays(30));

        var exercise = new TrainingPlanExercise
        {
            TrainingPlanExerciseId = Guid.NewGuid(),
            TrainingPlanId = plan.TrainingPlanId,
            ExerciseName = "Barbell Squat",
            WeekNumber = 1,
            DayNumber = 1,
            WorkoutCompletionLogs = new List<WorkoutCompletionLog>
            {
                new WorkoutCompletionLog { TraineeId = traineeId, Status = "COMPLETED" }
            }
        };
        plan.TrainingPlanExercises.Add(exercise);

        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.GetTraineeWorkoutPlanAsync(traineeId, orderDetail.OrderDetailsId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(plan.TrainingPlanId, result.TrainingPlanId);
        Assert.Equal(orderDetail.OrderDetailsId, result.OrderDetailsId);
        Assert.Equal("60-Day Hypertrophy Plan", result.PlanTitle);
        Assert.Equal("IN_PROGRESS", result.PlanStatus);
        Assert.Equal(1, result.TotalExercisesCount);
        Assert.Equal(1, result.CompletedExercisesCount);
        Assert.Equal(0, result.MissedExercisesCount);
        Assert.Equal(100.0, result.AdherenceRate);
        Assert.Single(result.Weeks);
        Assert.Single(result.Weeks[0].Days);
        Assert.Single(result.Weeks[0].Days[0].Exercises);
        Assert.True(result.Weeks[0].Days[0].Exercises[0].IsCompleted);
    }

    [Fact]
    public async Task UTCID02_GetTraineeWorkoutPlanAsync_PastUnloggedExercise_BackfillsIncompleteLogs()
    {
        // Arrange (Exercise scheduled 2 days ago without logs)
        var traineeId = Guid.NewGuid();
        var businessToday = TrainingPlanService.GetBusinessToday();
        var (plan, orderDetail) = CreateFullPlanHierarchy(
            traineeId, planStartDate: businessToday.AddDays(-2), planEndDate: businessToday.AddDays(28));

        var pastExercise = new TrainingPlanExercise
        {
            TrainingPlanExerciseId = Guid.NewGuid(),
            TrainingPlanId = plan.TrainingPlanId,
            ExerciseName = "Deadlift",
            WeekNumber = 1,
            DayNumber = 1, // offset 0 -> 2 days ago
            WorkoutCompletionLogs = new List<WorkoutCompletionLog>() // no logs yet
        };
        plan.TrainingPlanExercises.Add(pastExercise);

        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.GetTraineeWorkoutPlanAsync(traineeId, orderDetail.OrderDetailsId);

        // Assert
        Assert.NotNull(result);
        _mockPlanRepo.Verify(r => r.AddWorkoutLogAsync(It.Is<WorkoutCompletionLog>(l =>
            l.TrainingPlanExerciseId == pastExercise.TrainingPlanExerciseId &&
            l.TraineeId == traineeId &&
            l.Status == "INCOMPLETE"), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID03_GetTraineeWorkoutPlanAsync_PlanNull_ReturnsPendingCoachPlaceholder()
    {
        // Arrange (Plan is null, coach has not created plan yet)
        var traineeId = Guid.NewGuid();
        var orderDetailsId = Guid.NewGuid();
        var buyer = new User { UserId = traineeId, FullName = "Trainee Nguyen" };
        var order = new Order { OrderId = Guid.NewGuid(), BuyerId = traineeId, Buyer = buyer, OrderStatus = "PAID" };
        var orderDetail = new OrderDetail
        {
            OrderDetailsId = orderDetailsId,
            OrderId = order.OrderId,
            Order = order,
            PackageTitle = "30-Day Lean Muscle",
            PackageType = "BOTH",
            CoachName = "Coach Tran"
        };

        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(orderDetailsId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainingPlan?)null);
        _mockPlanRepo.Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(orderDetailsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderDetail);

        // Act
        var result = await _service.GetTraineeWorkoutPlanAsync(traineeId, orderDetailsId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(Guid.Empty, result.TrainingPlanId);
        Assert.Equal(orderDetailsId, result.OrderDetailsId);
        Assert.Equal("PENDING_COACH", result.PlanStatus);
        Assert.Equal("Training Plan Not Ready", result.PlanTitle);
        Assert.Contains("coach is currently creating your personalized training program", result.PlanDescription);
        Assert.Equal("Coach Tran", result.CoachName);
    }

    [Fact]
    public async Task UTCID04_GetTraineeWorkoutPlanAsync_PlanStatusDraft_ReturnsDraftPlanWithEmptyWeeks()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var (plan, orderDetail) = CreateFullPlanHierarchy(traineeId, planStatus: "DRAFT");

        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.GetTraineeWorkoutPlanAsync(traineeId, orderDetail.OrderDetailsId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("DRAFT", result.PlanStatus);
        Assert.Empty(result.Weeks);
        _mockPlanRepo.Verify(r => r.AddWorkoutLogAsync(It.IsAny<WorkoutCompletionLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UTCID05_GetTraineeWorkoutPlanAsync_PlanStatusPendingCoach_ReturnsHeaderWithEmptyWeeks()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var (plan, orderDetail) = CreateFullPlanHierarchy(traineeId, planStatus: "PENDING_COACH");

        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.GetTraineeWorkoutPlanAsync(traineeId, orderDetail.OrderDetailsId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("PENDING_COACH", result.PlanStatus);
        Assert.Empty(result.Weeks);
    }

    [Fact]
    public async Task UTCID06_GetTraineeWorkoutPlanByPackageAsync_ActivePlan_ReturnsPlanSuccessfully()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var businessToday = TrainingPlanService.GetBusinessToday();
        var (plan, orderDetail) = CreateFullPlanHierarchy(
            traineeId, planStartDate: businessToday, planEndDate: businessToday.AddDays(30));

        _mockPlanRepo.Setup(r => r.GetTraineePlanByPackageIdWithLogsAsync(packageId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.GetTraineeWorkoutPlanByPackageAsync(traineeId, packageId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(plan.TrainingPlanId, result.TrainingPlanId);
        Assert.Equal("60-Day Hypertrophy Plan", result.PlanTitle);
    }

    [Fact]
    public async Task UTCID07_GetTraineeWorkoutPlanAsync_AllExercisesCompleted_AdherenceRateIs100()
    {
        // Arrange (2 exercises, both COMPLETED)
        var traineeId = Guid.NewGuid();
        var businessToday = TrainingPlanService.GetBusinessToday();
        var (plan, orderDetail) = CreateFullPlanHierarchy(
            traineeId, planStartDate: businessToday, planEndDate: businessToday.AddDays(30));

        var ex1 = new TrainingPlanExercise
        {
            TrainingPlanExerciseId = Guid.NewGuid(),
            TrainingPlanId = plan.TrainingPlanId,
            WeekNumber = 1,
            DayNumber = 1,
            WorkoutCompletionLogs = new List<WorkoutCompletionLog> { new() { TraineeId = traineeId, Status = "COMPLETED" } }
        };
        var ex2 = new TrainingPlanExercise
        {
            TrainingPlanExerciseId = Guid.NewGuid(),
            TrainingPlanId = plan.TrainingPlanId,
            WeekNumber = 1,
            DayNumber = 1,
            WorkoutCompletionLogs = new List<WorkoutCompletionLog> { new() { TraineeId = traineeId, Status = "COMPLETED" } }
        };
        plan.TrainingPlanExercises.Add(ex1);
        plan.TrainingPlanExercises.Add(ex2);

        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.GetTraineeWorkoutPlanAsync(traineeId, orderDetail.OrderDetailsId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalExercisesCount);
        Assert.Equal(2, result.CompletedExercisesCount);
        Assert.Equal(0, result.MissedExercisesCount);
        Assert.Equal(100.0, result.AdherenceRate);
    }

    [Fact]
    public async Task UTCID08_GetTraineeWorkoutPlanAsync_PartialCompletedAndMissed_CalculatesAdherenceCorrectly()
    {
        // Arrange (1 COMPLETED, 1 INCOMPLETE -> adherence = 50.0%)
        var traineeId = Guid.NewGuid();
        var businessToday = TrainingPlanService.GetBusinessToday();
        var (plan, orderDetail) = CreateFullPlanHierarchy(
            traineeId, planStartDate: businessToday, planEndDate: businessToday.AddDays(30));

        var ex1 = new TrainingPlanExercise
        {
            TrainingPlanExerciseId = Guid.NewGuid(),
            TrainingPlanId = plan.TrainingPlanId,
            WeekNumber = 1,
            DayNumber = 1,
            WorkoutCompletionLogs = new List<WorkoutCompletionLog> { new() { TraineeId = traineeId, Status = "COMPLETED" } }
        };
        var ex2 = new TrainingPlanExercise
        {
            TrainingPlanExerciseId = Guid.NewGuid(),
            TrainingPlanId = plan.TrainingPlanId,
            WeekNumber = 1,
            DayNumber = 1,
            WorkoutCompletionLogs = new List<WorkoutCompletionLog> { new() { TraineeId = traineeId, Status = "INCOMPLETE" } }
        };
        plan.TrainingPlanExercises.Add(ex1);
        plan.TrainingPlanExercises.Add(ex2);

        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.GetTraineeWorkoutPlanAsync(traineeId, orderDetail.OrderDetailsId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalExercisesCount);
        Assert.Equal(1, result.CompletedExercisesCount);
        Assert.Equal(1, result.MissedExercisesCount);
        Assert.Equal(50.0, result.AdherenceRate);
    }

    [Fact]
    public async Task UTCID09_GetTraineeWorkoutPlanAsync_OrderStatusCompleted_AllowsAccess()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var (plan, orderDetail) = CreateFullPlanHierarchy(traineeId, orderStatus: "COMPLETED");

        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.GetTraineeWorkoutPlanAsync(traineeId, orderDetail.OrderDetailsId);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task UTCID10_GetTraineeWorkoutPlanAsync_OrderStatusRefundPending_AllowsAccess()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var (plan, orderDetail) = CreateFullPlanHierarchy(traineeId, orderStatus: "REFUND_PENDING");

        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.GetTraineeWorkoutPlanAsync(traineeId, orderDetail.OrderDetailsId);

        // Assert
        Assert.NotNull(result);
    }

    #endregion

    #region Boundary Test Cases (UTCID11 - UTCID16)

    [Fact]
    public async Task UTCID11_GetTraineeWorkoutPlanAsync_ZeroExercisesInPlan_ReturnsZeroAdherence()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var (plan, orderDetail) = CreateFullPlanHierarchy(traineeId, exercises: new List<TrainingPlanExercise>());

        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.GetTraineeWorkoutPlanAsync(traineeId, orderDetail.OrderDetailsId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalExercisesCount);
        Assert.Equal(0, result.CompletedExercisesCount);
        Assert.Equal(0, result.MissedExercisesCount);
        Assert.Equal(0.0, result.AdherenceRate);
        Assert.Empty(result.Weeks);
    }

    [Fact]
    public async Task UTCID12_GetTraineeWorkoutPlanAsync_PlanWithoutStartDate_SkipsBackfillAndMarksDaysValid()
    {
        // Arrange (Flexible plan where StartDate is null)
        var traineeId = Guid.NewGuid();
        var (plan, orderDetail) = CreateFullPlanHierarchy(traineeId, planStartDate: null);

        var exercise = new TrainingPlanExercise
        {
            TrainingPlanExerciseId = Guid.NewGuid(),
            TrainingPlanId = plan.TrainingPlanId,
            WeekNumber = 1,
            DayNumber = 1,
            WorkoutCompletionLogs = new List<WorkoutCompletionLog>()
        };
        plan.TrainingPlanExercises.Add(exercise);

        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.GetTraineeWorkoutPlanAsync(traineeId, orderDetail.OrderDetailsId);

        // Assert
        Assert.NotNull(result);
        _mockPlanRepo.Verify(r => r.AddWorkoutLogAsync(It.IsAny<WorkoutCompletionLog>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.True(result.Weeks[0].Days[0].IsToday); // When no schedule, IsToday is true (markable)
    }

    [Fact]
    public async Task UTCID13_GetTraineeWorkoutPlanAsync_OrderDetailPackageTitleNull_FallsBackToPackageTitle()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var (plan, orderDetail) = CreateFullPlanHierarchy(traineeId);
        orderDetail.PackageTitle = null; // null snapshot
        orderDetail.Package!.Title = "Fallback Master Package";

        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.GetTraineeWorkoutPlanAsync(traineeId, orderDetail.OrderDetailsId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Fallback Master Package", result.PackageTitle);
    }

    [Fact]
    public async Task UTCID14_GetTraineeWorkoutPlanAsync_CoachNameNull_FallsBackToCoachFullName()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var (plan, orderDetail) = CreateFullPlanHierarchy(traineeId);
        orderDetail.CoachName = null;
        plan.Coach!.Coach!.FullName = "Coach Fallback Name";

        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.GetTraineeWorkoutPlanAsync(traineeId, orderDetail.OrderDetailsId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Coach Fallback Name", result.CoachName);
    }

    [Fact]
    public async Task UTCID15_GetTraineeWorkoutPlanAsync_EmptyTraineeId_ThrowsForbiddenException()
    {
        // Arrange
        var emptyTraineeId = Guid.Empty;
        var orderDetailsId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.GetTraineeWorkoutPlanAsync(emptyTraineeId, orderDetailsId));

        Assert.Equal("Invalid trainee identity.", ex.Message);
    }

    [Fact]
    public async Task UTCID16_GetTraineeWorkoutPlanAsync_EmptyOrderDetailsId_ThrowsNotFoundException()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var emptyOrderDetailsId = Guid.Empty;

        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(emptyOrderDetailsId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainingPlan?)null);
        _mockPlanRepo.Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(emptyOrderDetailsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderDetail?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetTraineeWorkoutPlanAsync(traineeId, emptyOrderDetailsId));

        Assert.Equal("Order detail not found or does not belong to you.", ex.Message);
    }

    #endregion

    #region Abnormal Test Cases (UTCID17 - UTCID22)

    [Fact]
    public async Task UTCID17_GetTraineeWorkoutPlanAsync_OrderDetailNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var nonExistentOrderDetailsId = Guid.NewGuid();

        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(nonExistentOrderDetailsId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainingPlan?)null);
        _mockPlanRepo.Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(nonExistentOrderDetailsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderDetail?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetTraineeWorkoutPlanAsync(traineeId, nonExistentOrderDetailsId));

        Assert.Equal("Order detail not found or does not belong to you.", ex.Message);
    }

    [Fact]
    public async Task UTCID18_GetTraineeWorkoutPlanAsync_OrderDetailBelongsToAnotherTrainee_ThrowsNotFoundException()
    {
        // Arrange
        var requesterTraineeId = Guid.NewGuid();
        var actualBuyerId = Guid.NewGuid();
        var orderDetailsId = Guid.NewGuid();
        var order = new Order { OrderId = Guid.NewGuid(), BuyerId = actualBuyerId };
        var orderDetail = new OrderDetail { OrderDetailsId = orderDetailsId, Order = order };

        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(orderDetailsId, requesterTraineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainingPlan?)null);
        _mockPlanRepo.Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(orderDetailsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderDetail);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetTraineeWorkoutPlanAsync(requesterTraineeId, orderDetailsId));

        Assert.Equal("Order detail not found or does not belong to you.", ex.Message);
    }

    [Fact]
    public async Task UTCID19_GetTraineeWorkoutPlanAsync_RefundedOrder_ThrowsForbiddenException()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var (plan, orderDetail) = CreateFullPlanHierarchy(traineeId, orderStatus: "REFUNDED");

        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.GetTraineeWorkoutPlanAsync(traineeId, orderDetail.OrderDetailsId));

        Assert.Equal("This package has been refunded and its workout plan is no longer accessible.", ex.Message);
    }

    [Fact]
    public async Task UTCID20_GetTraineeWorkoutPlanAsync_DisallowedOrderStatus_ThrowsForbiddenException()
    {
        // Arrange (Order status "PENDING" is not allowed)
        var traineeId = Guid.NewGuid();
        var (plan, orderDetail) = CreateFullPlanHierarchy(traineeId, orderStatus: "PENDING");

        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.GetTraineeWorkoutPlanAsync(traineeId, orderDetail.OrderDetailsId));

        Assert.Contains("Workout plan is not accessible for order with status 'PENDING'", ex.Message);
    }

    [Fact]
    public async Task UTCID21_GetTraineeWorkoutPlanByPackageAsync_NoPlanFound_ThrowsNotFoundException()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var packageId = Guid.NewGuid();

        _mockPlanRepo.Setup(r => r.GetTraineePlanByPackageIdWithLogsAsync(packageId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainingPlan?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetTraineeWorkoutPlanByPackageAsync(traineeId, packageId));

        Assert.Equal("No active training plan found for this purchased package.", ex.Message);
    }

    [Fact]
    public async Task UTCID22_GetTraineeWorkoutPlanAsync_OperationCanceled_ThrowsOperationCanceledException()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var orderDetailsId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockPlanRepo.Setup(r => r.GetTraineePlanWithLogsAsync(orderDetailsId, traineeId, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.GetTraineeWorkoutPlanAsync(traineeId, orderDetailsId, cts.Token));
    }

    #endregion
}
