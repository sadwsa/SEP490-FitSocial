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
/// Unit test suite for Monitor Trainee Workout Progress feature:
/// TrainingPlanService.GetCoachTraineeProgressAsync(Guid coachId, Guid orderDetailsId, CancellationToken ct)
/// Covers:
/// - Normal test cases (UTCID01 - UTCID10): Active plan, backfill on open, no plan created placeholder, draft/pending coach, activity logs, adherence rate, allowed order statuses
/// - Boundary test cases (UTCID11 - UTCID16): 0 exercises, no start date, trainee fallback names, package title fallback, Guid.Empty boundaries
/// - Abnormal test cases (UTCID17 - UTCID22): MEAL only package (with/without plan), not found order detail, unauthorized coach, refunded package, cancellation token
/// </summary>
public class MonitorTraineeWorkoutProgressServiceTests
{
    private readonly Mock<ITrainingPlanRepository> _mockPlanRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<INotificationRepository> _mockNotificationRepo;
    private readonly TrainingPlanService _service;

    public MonitorTraineeWorkoutProgressServiceTests()
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

    private static (TrainingPlan Plan, OrderDetail OrderDetail) CreateCoachPlanHierarchy(
        Guid coachId,
        Guid? traineeId = null,
        DateTime? planStartDate = null,
        DateTime? planEndDate = null,
        string orderStatus = "PAID",
        string planStatus = "IN_PROGRESS",
        string packageType = "BOTH",
        List<TrainingPlanExercise>? exercises = null)
    {
        var tId = traineeId ?? Guid.NewGuid();
        var orderDetailsId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var packageId = Guid.NewGuid();

        var buyer = new User
        {
            UserId = tId,
            FullName = "Trainee Nguyen Van B",
            Email = "trainee_b@fitsocial.com",
            AvatarUrl = "https://cdn.fitsocial.com/avatar_b.jpg",
            PhoneNumber = "0987654321"
        };

        var order = new Order
        {
            OrderId = orderId,
            BuyerId = tId,
            Buyer = buyer,
            CoachId = coachId,
            OrderStatus = orderStatus
        };

        var package = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = "Iron Muscle 90 Days",
            PackageType = packageType
        };

        var orderDetail = new OrderDetail
        {
            OrderDetailsId = orderDetailsId,
            OrderId = orderId,
            Order = order,
            PackageId = packageId,
            Package = package,
            PackageTitle = "Iron Muscle 90 Days",
            PackageType = packageType,
            CoachName = "Coach John Doe"
        };

        var plan = new TrainingPlan
        {
            TrainingPlanId = Guid.NewGuid(),
            CoachId = coachId,
            OrderDetailsId = orderDetailsId,
            OrderDetails = orderDetail,
            StartDate = planStartDate,
            EndDate = planEndDate,
            Status = planStatus,
            Title = "90-Day Advanced Muscle Routine",
            TrainingPlanExercises = exercises ?? new List<TrainingPlanExercise>()
        };

        return (plan, orderDetail);
    }

    #endregion

    #region Normal Test Cases (UTCID01 - UTCID10)

    [Fact]
    public async Task UTCID01_GetCoachTraineeProgressAsync_ActivePlanWithExercises_ReturnsFullTraineeProgress()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var businessToday = TrainingPlanService.GetBusinessToday();
        var (plan, orderDetail) = CreateCoachPlanHierarchy(
            coachId, traineeId, planStartDate: businessToday, planEndDate: businessToday.AddDays(30));

        var exercise = new TrainingPlanExercise
        {
            TrainingPlanExerciseId = Guid.NewGuid(),
            TrainingPlanId = plan.TrainingPlanId,
            ExerciseName = "Barbell Bench Press",
            WeekNumber = 1,
            DayNumber = 1,
            WorkoutCompletionLogs = new List<WorkoutCompletionLog>
            {
                new()
                {
                    WorkoutCompletionLogId = Guid.NewGuid(),
                    TraineeId = traineeId,
                    Status = "COMPLETED",
                    Notes = "Lifted 80kg successfully",
                    CreatedAt = DateTime.UtcNow
                }
            }
        };
        plan.TrainingPlanExercises.Add(exercise);

        _mockPlanRepo.Setup(r => r.GetCoachTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.GetCoachTraineeProgressAsync(coachId, orderDetail.OrderDetailsId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderDetail.OrderDetailsId, result.OrderDetailsId);
        Assert.Equal(traineeId, result.TraineeId);
        Assert.Equal("Trainee Nguyen Van B", result.TraineeName);
        Assert.Equal("trainee_b@fitsocial.com", result.TraineeEmail);
        Assert.Equal("0987654321", result.TraineePhone);
        Assert.Equal("Iron Muscle 90 Days", result.PackageTitle);
        Assert.Equal("90-Day Advanced Muscle Routine", result.PlanTitle);
        Assert.Equal("IN_PROGRESS", result.PlanStatus);
        Assert.Equal(1, result.TotalExercisesCount);
        Assert.Equal(1, result.CompletedExercisesCount);
        Assert.Equal(0, result.MissedExercisesCount);
        Assert.Equal(100.0, result.AdherenceRate);
        Assert.Single(result.RecentLogs);
        Assert.Equal("Barbell Bench Press", result.RecentLogs[0].ExerciseName);
        Assert.Equal("Lifted 80kg successfully", result.RecentLogs[0].Notes);
    }

    [Fact]
    public async Task UTCID02_GetCoachTraineeProgressAsync_PastUnloggedExercises_BackfillsIncompleteLogs()
    {
        // Arrange (Exercise scheduled 2 days ago without logs)
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var businessToday = TrainingPlanService.GetBusinessToday();
        var (plan, orderDetail) = CreateCoachPlanHierarchy(
            coachId, traineeId, planStartDate: businessToday.AddDays(-2), planEndDate: businessToday.AddDays(28));

        var pastExercise = new TrainingPlanExercise
        {
            TrainingPlanExerciseId = Guid.NewGuid(),
            TrainingPlanId = plan.TrainingPlanId,
            ExerciseName = "Barbell Squat",
            WeekNumber = 1,
            DayNumber = 1, // 2 days ago
            WorkoutCompletionLogs = new List<WorkoutCompletionLog>()
        };
        plan.TrainingPlanExercises.Add(pastExercise);

        _mockPlanRepo.Setup(r => r.GetCoachTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.GetCoachTraineeProgressAsync(coachId, orderDetail.OrderDetailsId);

        // Assert
        Assert.NotNull(result);
        _mockPlanRepo.Verify(r => r.AddWorkoutLogAsync(It.Is<WorkoutCompletionLog>(l =>
            l.TrainingPlanExerciseId == pastExercise.TrainingPlanExerciseId &&
            l.TraineeId == traineeId &&
            l.Status == "INCOMPLETE"), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID03_GetCoachTraineeProgressAsync_PlanNotCreatedYet_ReturnsNoPlanCreatedPlaceholder()
    {
        // Arrange (Plan is null, coach has not created plan yet)
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderDetailsId = Guid.NewGuid();
        var buyer = new User { UserId = traineeId, FullName = "Trainee Nguyen", Email = "trainee@fitsocial.com" };
        var order = new Order { OrderId = Guid.NewGuid(), BuyerId = traineeId, Buyer = buyer, CoachId = coachId, OrderStatus = "PAID" };
        var orderDetail = new OrderDetail
        {
            OrderDetailsId = orderDetailsId,
            OrderId = order.OrderId,
            Order = order,
            PackageTitle = "VIP Training Package",
            PackageType = "BOTH"
        };

        _mockPlanRepo.Setup(r => r.GetCoachTraineePlanWithLogsAsync(orderDetailsId, coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainingPlan?)null);
        _mockPlanRepo.Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(orderDetailsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderDetail);

        // Act
        var result = await _service.GetCoachTraineeProgressAsync(coachId, orderDetailsId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("No Plan Created", result.PlanTitle);
        Assert.Equal("DRAFT", result.PlanStatus);
        Assert.Equal(0, result.TotalExercisesCount);
        Assert.Equal(0, result.CompletedExercisesCount);
        Assert.Equal(0, result.AdherenceRate);
        Assert.Equal("Trainee Nguyen", result.TraineeName);
    }

    [Fact]
    public async Task UTCID04_GetCoachTraineeProgressAsync_PlanStatusDraft_SkipsBackfill()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var (plan, orderDetail) = CreateCoachPlanHierarchy(coachId, traineeId, planStatus: "DRAFT");

        _mockPlanRepo.Setup(r => r.GetCoachTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.GetCoachTraineeProgressAsync(coachId, orderDetail.OrderDetailsId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("DRAFT", result.PlanStatus);
        _mockPlanRepo.Verify(r => r.AddWorkoutLogAsync(It.IsAny<WorkoutCompletionLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UTCID05_GetCoachTraineeProgressAsync_PlanStatusPendingCoach_SkipsBackfill()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var (plan, orderDetail) = CreateCoachPlanHierarchy(coachId, traineeId, planStatus: "PENDING_COACH");

        _mockPlanRepo.Setup(r => r.GetCoachTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.GetCoachTraineeProgressAsync(coachId, orderDetail.OrderDetailsId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("PENDING_COACH", result.PlanStatus);
        _mockPlanRepo.Verify(r => r.AddWorkoutLogAsync(It.IsAny<WorkoutCompletionLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UTCID06_GetCoachTraineeProgressAsync_RecentLogs_OrderedByCreatedAtDescendingAndMax50()
    {
        // Arrange (Create 55 logs, should only take latest 50)
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var (plan, orderDetail) = CreateCoachPlanHierarchy(coachId, traineeId);

        var exercise = new TrainingPlanExercise
        {
            TrainingPlanExerciseId = Guid.NewGuid(),
            TrainingPlanId = plan.TrainingPlanId,
            ExerciseName = "Push-ups",
            WorkoutCompletionLogs = new List<WorkoutCompletionLog>()
        };

        var baseTime = DateTime.UtcNow;
        for (int i = 0; i < 55; i++)
        {
            exercise.WorkoutCompletionLogs.Add(new WorkoutCompletionLog
            {
                WorkoutCompletionLogId = Guid.NewGuid(),
                TraineeId = traineeId,
                Status = "COMPLETED",
                CreatedAt = baseTime.AddMinutes(i)
            });
        }
        plan.TrainingPlanExercises.Add(exercise);

        _mockPlanRepo.Setup(r => r.GetCoachTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.GetCoachTraineeProgressAsync(coachId, orderDetail.OrderDetailsId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(50, result.RecentLogs.Count);
        Assert.True(result.RecentLogs[0].CompletedAt >= result.RecentLogs[1].CompletedAt);
    }

    [Fact]
    public async Task UTCID07_GetCoachTraineeProgressAsync_AllExercisesCompleted_AdherenceRateIs100()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var (plan, orderDetail) = CreateCoachPlanHierarchy(coachId, traineeId);

        var ex = new TrainingPlanExercise
        {
            TrainingPlanExerciseId = Guid.NewGuid(),
            TrainingPlanId = plan.TrainingPlanId,
            WeekNumber = 1,
            DayNumber = 1,
            WorkoutCompletionLogs = new List<WorkoutCompletionLog> { new() { TraineeId = traineeId, Status = "COMPLETED" } }
        };
        plan.TrainingPlanExercises.Add(ex);

        _mockPlanRepo.Setup(r => r.GetCoachTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.GetCoachTraineeProgressAsync(coachId, orderDetail.OrderDetailsId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalExercisesCount);
        Assert.Equal(1, result.CompletedExercisesCount);
        Assert.Equal(100.0, result.AdherenceRate);
    }

    [Fact]
    public async Task UTCID08_GetCoachTraineeProgressAsync_PartialCompletedAndMissed_CalculatesAdherenceCorrectly()
    {
        // Arrange (1 COMPLETED, 1 INCOMPLETE -> 50.0%)
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var (plan, orderDetail) = CreateCoachPlanHierarchy(coachId, traineeId);

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

        _mockPlanRepo.Setup(r => r.GetCoachTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.GetCoachTraineeProgressAsync(coachId, orderDetail.OrderDetailsId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalExercisesCount);
        Assert.Equal(1, result.CompletedExercisesCount);
        Assert.Equal(1, result.MissedExercisesCount);
        Assert.Equal(50.0, result.AdherenceRate);
    }

    [Fact]
    public async Task UTCID09_GetCoachTraineeProgressAsync_OrderStatusCompleted_AllowsAccess()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var (plan, orderDetail) = CreateCoachPlanHierarchy(coachId, orderStatus: "COMPLETED");

        _mockPlanRepo.Setup(r => r.GetCoachTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.GetCoachTraineeProgressAsync(coachId, orderDetail.OrderDetailsId);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task UTCID10_GetCoachTraineeProgressAsync_OrderStatusRefundPending_AllowsAccess()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var (plan, orderDetail) = CreateCoachPlanHierarchy(coachId, orderStatus: "REFUND_PENDING");

        _mockPlanRepo.Setup(r => r.GetCoachTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.GetCoachTraineeProgressAsync(coachId, orderDetail.OrderDetailsId);

        // Assert
        Assert.NotNull(result);
    }

    #endregion

    #region Boundary Test Cases (UTCID11 - UTCID16)

    [Fact]
    public async Task UTCID11_GetCoachTraineeProgressAsync_ZeroExercisesInPlan_AdherenceRateIsZero()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var (plan, orderDetail) = CreateCoachPlanHierarchy(coachId, exercises: new List<TrainingPlanExercise>());

        _mockPlanRepo.Setup(r => r.GetCoachTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.GetCoachTraineeProgressAsync(coachId, orderDetail.OrderDetailsId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalExercisesCount);
        Assert.Equal(0, result.AdherenceRate);
        Assert.Empty(result.Weeks);
        Assert.Empty(result.RecentLogs);
    }

    [Fact]
    public async Task UTCID12_GetCoachTraineeProgressAsync_PlanWithoutStartDate_SkipsBackfill()
    {
        // Arrange (StartDate is null)
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var (plan, orderDetail) = CreateCoachPlanHierarchy(coachId, traineeId, planStartDate: null);

        var ex = new TrainingPlanExercise
        {
            TrainingPlanExerciseId = Guid.NewGuid(),
            TrainingPlanId = plan.TrainingPlanId,
            WeekNumber = 1,
            DayNumber = 1
        };
        plan.TrainingPlanExercises.Add(ex);

        _mockPlanRepo.Setup(r => r.GetCoachTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.GetCoachTraineeProgressAsync(coachId, orderDetail.OrderDetailsId);

        // Assert
        Assert.NotNull(result);
        _mockPlanRepo.Verify(r => r.AddWorkoutLogAsync(It.IsAny<WorkoutCompletionLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UTCID13_GetCoachTraineeProgressAsync_TraineeFullNameNull_FallsBackToDefaultName()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var (plan, orderDetail) = CreateCoachPlanHierarchy(coachId);
        orderDetail.Order!.Buyer!.FullName = null;

        _mockPlanRepo.Setup(r => r.GetCoachTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.GetCoachTraineeProgressAsync(coachId, orderDetail.OrderDetailsId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Trainee", result.TraineeName);
    }

    [Fact]
    public async Task UTCID14_GetCoachTraineeProgressAsync_PackageTitleNull_FallsBackToPackageEntityTitle()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var (plan, orderDetail) = CreateCoachPlanHierarchy(coachId);
        orderDetail.PackageTitle = null;
        orderDetail.Package!.Title = "Fallback Package Title";

        _mockPlanRepo.Setup(r => r.GetCoachTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.GetCoachTraineeProgressAsync(coachId, orderDetail.OrderDetailsId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Fallback Package Title", result.PackageTitle);
    }

    [Fact]
    public async Task UTCID15_GetCoachTraineeProgressAsync_EmptyCoachId_ThrowsForbiddenException()
    {
        // Arrange
        var emptyCoachId = Guid.Empty;
        var orderDetailsId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.GetCoachTraineeProgressAsync(emptyCoachId, orderDetailsId));

        Assert.Equal("Invalid coach identity.", ex.Message);
    }

    [Fact]
    public async Task UTCID16_GetCoachTraineeProgressAsync_EmptyOrderDetailsId_ThrowsNotFoundException()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var emptyOrderDetailsId = Guid.Empty;

        _mockPlanRepo.Setup(r => r.GetCoachTraineePlanWithLogsAsync(emptyOrderDetailsId, coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainingPlan?)null);
        _mockPlanRepo.Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(emptyOrderDetailsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderDetail?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetCoachTraineeProgressAsync(coachId, emptyOrderDetailsId));

        Assert.Equal("Order detail not found or does not belong to you.", ex.Message);
    }

    #endregion

    #region Abnormal Test Cases (UTCID17 - UTCID22)

    [Fact]
    public async Task UTCID17_GetCoachTraineeProgressAsync_MealOnlyPackageWithPlan_ThrowsValidationException()
    {
        // Arrange (Package is MEAL only, does not have workout plan)
        var coachId = Guid.NewGuid();
        var (plan, orderDetail) = CreateCoachPlanHierarchy(coachId, packageType: "MEAL");

        _mockPlanRepo.Setup(r => r.GetCoachTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.GetCoachTraineeProgressAsync(coachId, orderDetail.OrderDetailsId));

        Assert.Equal("This package is MEAL only and does not include a workout/training plan.", ex.Message);
    }

    [Fact]
    public async Task UTCID18_GetCoachTraineeProgressAsync_MealOnlyPackageWithoutPlan_ThrowsValidationException()
    {
        // Arrange (Plan is null, orderDetail packageType is MEAL)
        var coachId = Guid.NewGuid();
        var orderDetailsId = Guid.NewGuid();
        var order = new Order { OrderId = Guid.NewGuid(), CoachId = coachId, OrderStatus = "PAID" };
        var orderDetail = new OrderDetail
        {
            OrderDetailsId = orderDetailsId,
            Order = order,
            PackageType = "MEAL"
        };

        _mockPlanRepo.Setup(r => r.GetCoachTraineePlanWithLogsAsync(orderDetailsId, coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainingPlan?)null);
        _mockPlanRepo.Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(orderDetailsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderDetail);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.GetCoachTraineeProgressAsync(coachId, orderDetailsId));

        Assert.Equal("This package is MEAL only and does not include a workout/training plan.", ex.Message);
    }

    [Fact]
    public async Task UTCID19_GetCoachTraineeProgressAsync_OrderDetailNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var nonExistentOrderDetailsId = Guid.NewGuid();

        _mockPlanRepo.Setup(r => r.GetCoachTraineePlanWithLogsAsync(nonExistentOrderDetailsId, coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainingPlan?)null);
        _mockPlanRepo.Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(nonExistentOrderDetailsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderDetail?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetCoachTraineeProgressAsync(coachId, nonExistentOrderDetailsId));

        Assert.Equal("Order detail not found or does not belong to you.", ex.Message);
    }

    [Fact]
    public async Task UTCID20_GetCoachTraineeProgressAsync_OrderDetailBelongsToAnotherCoach_ThrowsNotFoundException()
    {
        // Arrange
        var maliciousCoachId = Guid.NewGuid();
        var actualCoachId = Guid.NewGuid();
        var orderDetailsId = Guid.NewGuid();
        var order = new Order { OrderId = Guid.NewGuid(), CoachId = actualCoachId, OrderStatus = "PAID" };
        var orderDetail = new OrderDetail { OrderDetailsId = orderDetailsId, Order = order };

        _mockPlanRepo.Setup(r => r.GetCoachTraineePlanWithLogsAsync(orderDetailsId, maliciousCoachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainingPlan?)null);
        _mockPlanRepo.Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(orderDetailsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderDetail);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetCoachTraineeProgressAsync(maliciousCoachId, orderDetailsId));

        Assert.Equal("Order detail not found or does not belong to you.", ex.Message);
    }

    [Fact]
    public async Task UTCID21_GetCoachTraineeProgressAsync_RefundedOrder_ThrowsForbiddenException()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var (plan, orderDetail) = CreateCoachPlanHierarchy(coachId, orderStatus: "REFUNDED");

        _mockPlanRepo.Setup(r => r.GetCoachTraineePlanWithLogsAsync(orderDetail.OrderDetailsId, coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.GetCoachTraineeProgressAsync(coachId, orderDetail.OrderDetailsId));

        Assert.Equal("This package has been refunded and its workout plan is no longer accessible.", ex.Message);
    }

    [Fact]
    public async Task UTCID22_GetCoachTraineeProgressAsync_OperationCanceled_ThrowsOperationCanceledException()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var orderDetailsId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockPlanRepo.Setup(r => r.GetCoachTraineePlanWithLogsAsync(orderDetailsId, coachId, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.GetCoachTraineeProgressAsync(coachId, orderDetailsId, cts.Token));
    }

    #endregion
}

