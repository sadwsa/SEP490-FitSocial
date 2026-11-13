using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.TrainingPlan;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class CoachTraineeProgressServiceTests
{
    private readonly Mock<ITrainingPlanRepository> _repo = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private static readonly Guid CoachId = Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA");
    private static readonly Guid TraineeId = Guid.Parse("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB");
    private static readonly Guid OrderDetailsId = Guid.Parse("CCCCCCCC-CCCC-CCCC-CCCC-CCCCCCCCCCCC");
    private static readonly Guid PlanId = Guid.Parse("DDDDDDDD-DDDD-DDDD-DDDD-DDDDDDDDDDDD");
    private static readonly Guid PackageId = Guid.Parse("EEEEEEEE-EEEE-EEEE-EEEE-EEEEEEEEEEEE");
    private static readonly Guid Ex1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Ex2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Ex3 = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private TrainingPlanService CreateSut() => new TrainingPlanService(_repo.Object, _uow.Object);

    private static WorkoutCompletionLog MakeLog(Guid logId, Guid traineeId, Guid exerciseId, string status, DateTime createdAt)
    {
        return new WorkoutCompletionLog
        {
            WorkoutCompletionLogId = logId,
            TraineeId = traineeId,
            TrainingPlanExerciseId = exerciseId,
            Status = status,
            CreatedAt = createdAt
        };
    }

    private static TrainingPlan MakePlanWithExercises(DateTime? startDate, DateTime? endDate, string status,
        params (Guid exerciseId, short week, short day, string name, string? logStatus, DateTime? logCreatedAt)[] exercises)
    {
        var plan = new TrainingPlan
        {
            TrainingPlanId = PlanId,
            CoachId = CoachId,
            OrderDetailsId = OrderDetailsId,
            Title = "Strength Builder",
            Description = "8-week strength program",
            StartDate = startDate,
            EndDate = endDate,
            Status = status,
            Coach = new CoachProfile { CoachId = CoachId },
            OrderDetails = new OrderDetail
            {
                OrderDetailsId = OrderDetailsId,
                PackageId = PackageId,
                PackageTitle = "Monthly Strength",
                Order = new Order
                {
                    OrderId = Guid.NewGuid(),
                    BuyerId = TraineeId,
                    CoachId = CoachId,
                    Buyer = new User
                    {
                        UserId = TraineeId,
                        FullName = "Nguyen Van Trainee",
                        Email = "trainee@example.com",
                        AvatarUrl = "trainee-avatar.png",
                        PhoneNumber = "0909123456"
                    },
                    Coach = new CoachProfile { CoachId = CoachId }
                },
                Package = new TrainingPackage
                {
                    PackageId = PackageId,
                    CoachId = CoachId,
                    Title = "Monthly Strength",
                    DurationDays = 56
                }
            },
            TrainingPlanExercises = new List<TrainingPlanExercise>()
        };

        foreach (var (exId, week, day, name, logStatus, logCreatedAt) in exercises)
        {
            var logs = new List<WorkoutCompletionLog>();
            if (logStatus != null && logCreatedAt.HasValue)
            {
                logs.Add(MakeLog(Guid.NewGuid(), TraineeId, exId, logStatus, logCreatedAt.Value));
            }

            plan.TrainingPlanExercises.Add(new TrainingPlanExercise
            {
                TrainingPlanExerciseId = exId,
                TrainingPlanId = PlanId,
                WeekNumber = week,
                DayNumber = day,
                ExerciseName = name,
                Sets = 3,
                Reps = 10,
                SortOrder = 1,
                WorkoutCompletionLogs = logs
            });
        }

        return plan;
    }

    #region GetCoachTraineeProgressAsync

    [Fact]
    public async Task GetCoachTraineeProgressAsync_CoachIdEmpty_ThrowsForbiddenException()
    {
        _repo.Reset();
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.GetCoachTraineeProgressAsync(Guid.Empty, OrderDetailsId, CancellationToken.None));
    }

    [Fact]
    public async Task GetCoachTraineeProgressAsync_PlanNull_OdNull_ThrowsNotFoundException()
    {
        _repo.Reset();
        _repo.Setup(r => r.GetCoachTraineePlanWithLogsAsync(OrderDetailsId, CoachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainingPlan?)null);
        _repo.Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(OrderDetailsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderDetail?)null);

        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetCoachTraineeProgressAsync(CoachId, OrderDetailsId, CancellationToken.None));
    }

    [Fact]
    public async Task GetCoachTraineeProgressAsync_PlanNull_OdNotBelongingToCoach_ThrowsNotFoundException()
    {
        _repo.Reset();
        var otherCoach = Guid.Parse("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF");
        _repo.Setup(r => r.GetCoachTraineePlanWithLogsAsync(OrderDetailsId, CoachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainingPlan?)null);
        _repo.Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(OrderDetailsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderDetail
            {
                OrderDetailsId = OrderDetailsId,
                Order = new Order { BuyerId = TraineeId, CoachId = otherCoach },
                Package = new TrainingPackage { CoachId = otherCoach }
            });

        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetCoachTraineeProgressAsync(CoachId, OrderDetailsId, CancellationToken.None));
    }

    [Fact]
    public async Task GetCoachTraineeProgressAsync_PlanNull_OdBelongingToCoach_ReturnsDraftDto()
    {
        _repo.Reset();
        _repo.Setup(r => r.GetCoachTraineePlanWithLogsAsync(OrderDetailsId, CoachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainingPlan?)null);
        _repo.Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(OrderDetailsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderDetail
            {
                OrderDetailsId = OrderDetailsId,
                PackageId = PackageId,
                PackageTitle = "Starter Pack",
                Order = new Order
                {
                    BuyerId = TraineeId,
                    CoachId = CoachId,
                    Buyer = new User { UserId = TraineeId, FullName = "Trainee One", Email = "one@example.com" }
                },
                Package = new TrainingPackage { PackageId = PackageId, CoachId = CoachId, Title = "Starter Pack" }
            });

        var sut = CreateSut();
        var result = await sut.GetCoachTraineeProgressAsync(CoachId, OrderDetailsId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(OrderDetailsId, result.OrderDetailsId);
        Assert.Equal(TraineeId, result.TraineeId);
        Assert.Equal("Trainee One", result.TraineeName);
        Assert.Equal("one@example.com", result.TraineeEmail);
        Assert.Equal(PackageId, result.PackageId);
        Assert.Equal("Starter Pack", result.PackageTitle);
        Assert.Equal("No Plan Created", result.PlanTitle);
        Assert.Equal("DRAFT", result.PlanStatus);
        Assert.Equal(0, result.TotalExercisesCount);
        Assert.Equal(0, result.CompletedExercisesCount);
        Assert.Empty(result.Weeks);
        Assert.Empty(result.RecentLogs);
    }

    [Fact]
    public async Task GetCoachTraineeProgressAsync_PlanExists_ReturnsProgressWithWeeksAndLogs()
    {
        _repo.Reset();
        var start = new DateTime(2026, 11, 2);
        var end = start.AddDays(13);

        var plan = MakePlanWithExercises(start, end, "ACTIVE",
            (Ex1, 1, 1, "Bench Press", "COMPLETED", new DateTime(2026, 11, 2, 8, 0, 0)),
            (Ex2, 1, 1, "Squat", "INCOMPLETE", new DateTime(2026, 11, 2, 9, 0, 0)),
            (Ex3, 1, 2, "Deadlift", "COMPLETED", new DateTime(2026, 11, 3, 8, 0, 0))
        );

        _repo.Setup(r => r.GetCoachTraineePlanWithLogsAsync(OrderDetailsId, CoachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var sut = CreateSut();
        var result = await sut.GetCoachTraineeProgressAsync(CoachId, OrderDetailsId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(OrderDetailsId, result.OrderDetailsId);
        Assert.Equal(TraineeId, result.TraineeId);
        Assert.Equal("Nguyen Van Trainee", result.TraineeName);
        Assert.Equal("trainee@example.com", result.TraineeEmail);
        Assert.Equal("trainee-avatar.png", result.TraineeAvatarUrl);
        Assert.Equal("0909123456", result.TraineePhone);
        Assert.Equal(PackageId, result.PackageId);
        Assert.Equal("Monthly Strength", result.PackageTitle);
        Assert.Equal(PlanId, result.TrainingPlanId);
        Assert.Equal("Strength Builder", result.PlanTitle);
        Assert.Equal("ACTIVE", result.PlanStatus);
        Assert.Equal(start, result.StartDate);
        Assert.Equal(end, result.EndDate);

        // 3 exercises across 1 week, 2 days
        Assert.Equal(1, result.Weeks.Count);
        var week = result.Weeks[0];
        Assert.Equal(1, week.WeekNumber);
        Assert.Equal(3, week.TotalExercises);
        Assert.Equal(2, week.CompletedExercises);
        Assert.Equal(2, week.Days.Count);

        Assert.Equal(3, result.TotalExercisesCount);
        Assert.Equal(2, result.CompletedExercisesCount);
        Assert.Equal(66.7, result.ProgressPercentage); // 2/3*100 = 66.666 -> 66.7

        // Recent logs: 3 logs ordered by CreatedAt desc
        Assert.Equal(3, result.RecentLogs.Count);
        var firstLog = result.RecentLogs[0];
        Assert.Equal("Deadlift", firstLog.ExerciseName);
        Assert.Equal((short)1, firstLog.WeekNumber);
        Assert.Equal((short)2, firstLog.DayNumber);
        Assert.Equal(new DateTime(2026, 11, 3, 8, 0, 0), firstLog.CompletedAt);
        Assert.Equal("COMPLETED", firstLog.Status);

        var lastLog = result.RecentLogs[2];
        Assert.Equal("Bench Press", lastLog.ExerciseName);
        Assert.Equal("COMPLETED", lastLog.Status);
    }

    [Fact]
    public async Task GetCoachTraineeProgressAsync_PlanExists_RecentLogsLimitedTo30()
    {
        _repo.Reset();
        var start = new DateTime(2026, 11, 2);
        var exercises = new (Guid exerciseId, short week, short day, string name, string? logStatus, DateTime? logCreatedAt)[35];
        var now = DateTime.UtcNow;
        for (int i = 0; i < 35; i++)
        {
            exercises[i] = (Guid.NewGuid(), 1, (short)(i % 5 + 1), $"Exercise {i}", "COMPLETED", now.AddMinutes(-i));
        }

        var plan = MakePlanWithExercises(start, start.AddDays(30), "ACTIVE", exercises);

        _repo.Setup(r => r.GetCoachTraineePlanWithLogsAsync(OrderDetailsId, CoachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var sut = CreateSut();
        var result = await sut.GetCoachTraineeProgressAsync(CoachId, OrderDetailsId, CancellationToken.None);

        // Only the latest 30 logs should be returned
        Assert.Equal(30, result.RecentLogs.Count);
        Assert.Equal(35, result.TotalExercisesCount);
        Assert.Equal(35, result.CompletedExercisesCount);
        Assert.Equal(100.0, result.ProgressPercentage);
    }

    #endregion
}
