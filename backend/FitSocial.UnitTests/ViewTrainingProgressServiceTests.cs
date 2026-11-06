using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.TrainingPlan;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.UnitTests.Common;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class ViewTrainingProgressServiceTests
{
    private readonly Mock<ITrainingPlanRepository> _trainingPlanRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public static TheoryData<ViewTrainingProgressTestCase> TestCases
    {
        get
        {
            var data = new TheoryData<ViewTrainingProgressTestCase>();
            var assembly = typeof(ViewTrainingProgressServiceTests).Assembly;
            using var stream = assembly.GetManifestResourceStream(
                "FitSocial.UnitTests.TestData.ViewTrainingProgressTestCases.csv");
            if (stream == null) return data;
            using var reader = new StreamReader(stream);

            var rows = CsvTestHelper.ParseCsv(reader.ReadToEnd());
            if (rows.Count <= 1) return data;

            var header = rows[0];
            var index = header.Select((name, i) => (name.Trim(), i)).ToDictionary(x => x.Item1, x => x.Item2);

            foreach (var row in rows.Skip(1))
            {
                if (row.All(string.IsNullOrWhiteSpace)) continue;

                data.Add(new ViewTrainingProgressTestCase
                {
                    UtcId = CsvTestHelper.Get(row, index, "UTCID"),
                    Type = CsvTestHelper.Get(row, index, "Type"),
                    Scenario = CsvTestHelper.Get(row, index, "Scenario"),
                    ExpectedExceptionType = CsvTestHelper.Get(row, index, "ExpectedExceptionType"),
                    ExpectedMessage = CsvTestHelper.Get(row, index, "ExpectedMessage")
                });
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public async Task ViewTrainingProgressTest(ViewTrainingProgressTestCase tc)
    {
        _trainingPlanRepository.Reset();
        _unitOfWork.Reset();

        var normalizedScenario = tc.Scenario.Trim().ToUpperInvariant();
        var traineeId = normalizedScenario == "INVALID_TRAINEE" ? Guid.Empty : Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var orderDetailsId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        var plan = BuildPlan(
            normalizedScenario,
            traineeId,
            otherUserId,
            coachId,
            orderDetailsId,
            packageId,
            planId);
        var orderDetail = BuildOrderDetail(
            normalizedScenario,
            traineeId,
            otherUserId,
            coachId,
            orderDetailsId,
            packageId);

        var addedLogs = new List<WorkoutCompletionLog>();
        SetupRepository(tc, plan, orderDetail, traineeId, orderDetailsId, addedLogs);

        var sut = new TrainingPlanService(_trainingPlanRepository.Object, _unitOfWork.Object);

        if (!string.Equals(tc.ExpectedExceptionType, "NONE", StringComparison.OrdinalIgnoreCase))
        {
            await AssertExpectedException(
                tc.ExpectedExceptionType,
                tc.ExpectedMessage,
                () => sut.GetTraineeWorkoutPlanAsync(traineeId, orderDetailsId));

            VerifyFailureCalls(normalizedScenario, traineeId, orderDetailsId);
            return;
        }

        var result = await sut.GetTraineeWorkoutPlanAsync(traineeId, orderDetailsId);
        VerifySuccessCalls(normalizedScenario, traineeId, orderDetailsId);

        switch (normalizedScenario)
        {
            case "FULL_PLAN_MAPPING":
                AssertFullPlanMapping(result, plan!, traineeId, coachId, orderDetailsId, packageId, planId);
                break;

            case "LOG_SELECTION":
                AssertLogSelection(result, plan!, traineeId);
                break;

            case "PAST_DAY_BACKFILL":
                AssertPastDayBackfill(result, plan!, addedLogs, traineeId, orderDetailsId);
                break;

            case "TODAY_FUTURE_SCHEDULE":
                AssertTodayFutureSchedule(result, plan!, addedLogs);
                break;

            case "PENDING_VALID_ORDER":
                AssertPendingValidOrder(result, coachId, orderDetailsId, packageId);
                break;

            case "EMPTY_PLAN":
                AssertEmptyPlan(result);
                break;

            case "OUTSIDE_PLAN_RANGE":
                AssertOutsidePlanRange(result, plan!);
                break;

            case "OTHER_TRAINEE_LOGS_IGNORED":
                AssertOtherTraineeLogsIgnored(result);
                break;

            case "PENDING_FALLBACKS":
                AssertPendingFallbacks(result, orderDetailsId);
                break;

            default:
                throw new InvalidOperationException(
                    $"{tc.UtcId}: unsupported success scenario \"{tc.Scenario}\".");
        }
    }

    private void SetupRepository(
        ViewTrainingProgressTestCase tc,
        TrainingPlan? plan,
        OrderDetail? orderDetail,
        Guid traineeId,
        Guid orderDetailsId,
        List<WorkoutCompletionLog> addedLogs)
    {
        var normalizedScenario = tc.Scenario.Trim().ToUpperInvariant();

        var planSetup = _trainingPlanRepository
            .Setup(r => r.GetTraineePlanWithLogsAsync(
                orderDetailsId,
                traineeId,
                It.IsAny<CancellationToken>()));

        if (normalizedScenario == "PLAN_QUERY_FAILURE")
        {
            planSetup.ThrowsAsync(new InvalidOperationException("Plan query failed."));
        }
        else
        {
            planSetup.ReturnsAsync(plan);
        }

        _trainingPlanRepository
            .Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(
                orderDetailsId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderDetail);

        var addSetup = _trainingPlanRepository
            .Setup(r => r.AddWorkoutLogAsync(
                It.IsAny<WorkoutCompletionLog>(),
                It.IsAny<CancellationToken>()))
            .Callback<WorkoutCompletionLog, CancellationToken>((log, _) => addedLogs.Add(log));

        if (normalizedScenario == "BACKFILL_ADD_FAILURE")
        {
            addSetup.ThrowsAsync(new InvalidOperationException("Add log failed."));
        }
        else
        {
            addSetup.Returns(Task.CompletedTask);
        }

        var saveSetup = _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()));
        if (normalizedScenario == "BACKFILL_SAVE_FAILURE")
        {
            saveSetup.ThrowsAsync(new DbUpdateException("Save failed."));
        }
        else
        {
            saveSetup.ReturnsAsync(1);
        }
    }

    private void VerifyFailureCalls(string scenario, Guid traineeId, Guid orderDetailsId)
    {
        switch (scenario)
        {
            case "INVALID_TRAINEE":
                _trainingPlanRepository.Verify(
                    r => r.GetTraineePlanWithLogsAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<Guid>(),
                        It.IsAny<CancellationToken>()),
                    Times.Never);
                _trainingPlanRepository.Verify(
                    r => r.GetOrderDetailWithOrderAndPackageAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<CancellationToken>()),
                    Times.Never);
                _trainingPlanRepository.Verify(
                    r => r.AddWorkoutLogAsync(
                        It.IsAny<WorkoutCompletionLog>(),
                        It.IsAny<CancellationToken>()),
                    Times.Never);
                _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
                break;

            case "PLAN_QUERY_FAILURE":
                _trainingPlanRepository.Verify(
                    r => r.GetTraineePlanWithLogsAsync(
                        orderDetailsId,
                        traineeId,
                        It.IsAny<CancellationToken>()),
                    Times.Once);
                _trainingPlanRepository.Verify(
                    r => r.GetOrderDetailWithOrderAndPackageAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<CancellationToken>()),
                    Times.Never);
                _trainingPlanRepository.Verify(
                    r => r.AddWorkoutLogAsync(
                        It.IsAny<WorkoutCompletionLog>(),
                        It.IsAny<CancellationToken>()),
                    Times.Never);
                _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
                break;

            case "ORDER_DETAIL_NOT_FOUND":
            case "OTHER_BUYER":
            case "ORDER_NAVIGATION_NULL":
                _trainingPlanRepository.Verify(
                    r => r.GetTraineePlanWithLogsAsync(
                        orderDetailsId,
                        traineeId,
                        It.IsAny<CancellationToken>()),
                    Times.Once);
                _trainingPlanRepository.Verify(
                    r => r.GetOrderDetailWithOrderAndPackageAsync(
                        orderDetailsId,
                        It.IsAny<CancellationToken>()),
                    Times.Once);
                _trainingPlanRepository.Verify(
                    r => r.AddWorkoutLogAsync(
                        It.IsAny<WorkoutCompletionLog>(),
                        It.IsAny<CancellationToken>()),
                    Times.Never);
                _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
                break;

            case "BACKFILL_ADD_FAILURE":
                _trainingPlanRepository.Verify(
                    r => r.GetTraineePlanWithLogsAsync(
                        orderDetailsId,
                        traineeId,
                        It.IsAny<CancellationToken>()),
                    Times.Once);
                _trainingPlanRepository.Verify(
                    r => r.GetOrderDetailWithOrderAndPackageAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<CancellationToken>()),
                    Times.Never);
                _trainingPlanRepository.Verify(
                    r => r.AddWorkoutLogAsync(
                        It.IsAny<WorkoutCompletionLog>(),
                        It.IsAny<CancellationToken>()),
                    Times.Once);
                _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
                break;

            case "BACKFILL_SAVE_FAILURE":
                _trainingPlanRepository.Verify(
                    r => r.GetTraineePlanWithLogsAsync(
                        orderDetailsId,
                        traineeId,
                        It.IsAny<CancellationToken>()),
                    Times.Once);
                _trainingPlanRepository.Verify(
                    r => r.GetOrderDetailWithOrderAndPackageAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<CancellationToken>()),
                    Times.Never);
                _trainingPlanRepository.Verify(
                    r => r.AddWorkoutLogAsync(
                        It.IsAny<WorkoutCompletionLog>(),
                        It.IsAny<CancellationToken>()),
                    Times.Once);
                _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
                break;

            default:
                throw new InvalidOperationException($"Unsupported failure scenario \"{scenario}\".");
        }

        _trainingPlanRepository.VerifyNoOtherCalls();
        _unitOfWork.VerifyNoOtherCalls();
    }

    private void VerifySuccessCalls(string scenario, Guid traineeId, Guid orderDetailsId)
    {
        _trainingPlanRepository.Verify(
            r => r.GetTraineePlanWithLogsAsync(
                orderDetailsId,
                traineeId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        switch (scenario)
        {
            case "PENDING_VALID_ORDER":
            case "PENDING_FALLBACKS":
                _trainingPlanRepository.Verify(
                    r => r.GetOrderDetailWithOrderAndPackageAsync(
                        orderDetailsId,
                        It.IsAny<CancellationToken>()),
                    Times.Once);
                _trainingPlanRepository.Verify(
                    r => r.AddWorkoutLogAsync(
                        It.IsAny<WorkoutCompletionLog>(),
                        It.IsAny<CancellationToken>()),
                    Times.Never);
                _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
                break;

            case "PAST_DAY_BACKFILL":
                _trainingPlanRepository.Verify(
                    r => r.GetOrderDetailWithOrderAndPackageAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<CancellationToken>()),
                    Times.Never);
                _trainingPlanRepository.Verify(
                    r => r.AddWorkoutLogAsync(
                        It.IsAny<WorkoutCompletionLog>(),
                        It.IsAny<CancellationToken>()),
                    Times.Exactly(2));
                _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
                break;

            case "FULL_PLAN_MAPPING":
            case "LOG_SELECTION":
            case "TODAY_FUTURE_SCHEDULE":
            case "EMPTY_PLAN":
            case "OUTSIDE_PLAN_RANGE":
            case "OTHER_TRAINEE_LOGS_IGNORED":
                _trainingPlanRepository.Verify(
                    r => r.GetOrderDetailWithOrderAndPackageAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<CancellationToken>()),
                    Times.Never);
                _trainingPlanRepository.Verify(
                    r => r.AddWorkoutLogAsync(
                        It.IsAny<WorkoutCompletionLog>(),
                        It.IsAny<CancellationToken>()),
                    Times.Never);
                _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
                break;

            default:
                throw new InvalidOperationException($"Unsupported success scenario \"{scenario}\".");
        }

        _trainingPlanRepository.VerifyNoOtherCalls();
        _unitOfWork.VerifyNoOtherCalls();
    }

    private static void AssertFullPlanMapping(
        TraineeWorkoutPlanProgressDto result,
        TrainingPlan plan,
        Guid traineeId,
        Guid coachId,
        Guid orderDetailsId,
        Guid packageId,
        Guid planId)
    {
        Assert.Equal(planId, result.TrainingPlanId);
        Assert.Equal(orderDetailsId, result.OrderDetailsId);
        Assert.Equal(packageId, result.PackageId);
        Assert.Equal("Strength Builder", result.PackageTitle);
        Assert.Equal("https://cdn.example/cover.jpg", result.PackageThumbnailUrl);
        Assert.Equal("12-Week Strength", result.PlanTitle);
        Assert.Equal("Build strength", result.PlanDescription);
        Assert.Equal("PUBLISHED", result.PlanStatus);
        Assert.Null(result.StartDate);
        Assert.Null(result.EndDate);
        Assert.Equal(coachId, result.CoachId);
        Assert.Equal("Coach Minh", result.CoachName);
        Assert.Equal("https://cdn.example/avatar.jpg", result.CoachAvatarUrl);
        AssertProgress(result, 5, 1, 20.0);

        Assert.Equal(new short[] { 1, 2 }, result.Weeks.Select(w => w.WeekNumber));
        var week1 = result.Weeks[0];
        var week2 = result.Weeks[1];
        Assert.Equal("Week 1", week1.Title);
        Assert.Equal("Week 2", week2.Title);
        Assert.Equal(3, week1.TotalExercises);
        Assert.Equal(1, week1.CompletedExercises);
        Assert.Equal(33.3, week1.ProgressPercentage, 1);
        Assert.Equal(2, week2.TotalExercises);
        Assert.Equal(0, week2.CompletedExercises);
        Assert.Equal(0.0, week2.ProgressPercentage, 1);

        Assert.Equal(new short[] { 1, 2 }, week1.Days.Select(d => d.DayNumber));
        var day1 = week1.Days[0];
        var day2 = week1.Days[1];
        Assert.Equal("Day 1", day1.Title);
        Assert.Equal(2, day1.TotalExercises);
        Assert.Equal(1, day1.CompletedExercises);
        Assert.False(day1.IsAllCompleted);
        Assert.Null(day1.ScheduledDate);
        Assert.True(day1.IsToday);
        Assert.Equal(new[] { "Squat", "Warm Up" }, day1.Exercises.Select(e => e.ExerciseName));
        Assert.Equal(1, day2.TotalExercises);
        Assert.Equal(0, day2.CompletedExercises);
        Assert.Null(day2.ScheduledDate);
        Assert.True(day2.IsToday);
        Assert.Equal(new[] { "Push Ups" }, day2.Exercises.Select(e => e.ExerciseName));

        var squat = day1.Exercises[0];
        var squatLog = plan.TrainingPlanExercises.Single(e => e.ExerciseName == "Squat")
            .WorkoutCompletionLogs.Single();
        Assert.Equal(planId, squat.TrainingPlanId);
        Assert.Equal((short)1, squat.WeekNumber);
        Assert.Equal((short)1, squat.DayNumber);
        Assert.Equal((short)5, squat.Sets);
        Assert.Equal((short)5, squat.Reps);
        Assert.Equal((short)15, squat.DurationMinutes);
        Assert.Equal((short)120, squat.RestSeconds);
        Assert.Equal("Depth", squat.Notes);
        Assert.True(squat.IsCompleted);
        Assert.Equal(squatLog.WorkoutCompletionLogId, squat.WorkoutCompletionLogId);
        Assert.Equal(squatLog.CreatedAt, squat.CompletedAt);
        Assert.Equal("Done", squat.TraineeNotes);
        Assert.Equal("COMPLETED", squat.Status);

        var warmUp = day1.Exercises[1];
        var warmUpLog = plan.TrainingPlanExercises.Single(e => e.ExerciseName == "Warm Up")
            .WorkoutCompletionLogs.Single();
        Assert.False(warmUp.IsCompleted);
        Assert.Equal(warmUpLog.WorkoutCompletionLogId, warmUp.WorkoutCompletionLogId);
        Assert.Equal(warmUpLog.CreatedAt, warmUp.CompletedAt);
        Assert.Equal("Skipped", warmUp.TraineeNotes);
        Assert.Equal("INCOMPLETE", warmUp.Status);

        var day3 = week2.Days[0];
        Assert.Equal(new[] { "Bench Press", "Deadlift" }, day3.Exercises.Select(e => e.ExerciseName));
        var deadlift = day3.Exercises[1];
        Assert.NotNull(deadlift.VideoTutorial);
        Assert.Equal("Deadlift Form", deadlift.VideoTutorial!.Title);
        Assert.Equal("Keep a neutral spine.", deadlift.VideoTutorial.Description);
        Assert.Equal("https://cdn.example/deadlift.mp4", deadlift.VideoTutorial.VideoUrl);
        Assert.Equal(coachId, deadlift.VideoTutorial.CoachId);
        Assert.Equal((short)4, deadlift.Sets);
        Assert.Equal((short)6, deadlift.Reps);
        Assert.Equal((short)20, deadlift.DurationMinutes);
        Assert.Equal((short)90, deadlift.RestSeconds);
        Assert.Equal("Heavy", deadlift.Notes);
        Assert.False(deadlift.IsCompleted);
        Assert.Null(deadlift.WorkoutCompletionLogId);
        Assert.Null(deadlift.CompletedAt);
        Assert.Null(deadlift.TraineeNotes);
        Assert.Null(deadlift.Status);
    }

    private static void AssertLogSelection(
        TraineeWorkoutPlanProgressDto result,
        TrainingPlan plan,
        Guid traineeId)
    {
        AssertProgress(result, 2, 1, 50.0);
        var day = Assert.Single(Assert.Single(result.Weeks).Days);

        var exercise1 = plan.TrainingPlanExercises.Single(e => e.ExerciseName == "Exercise 1");
        var completedLog = exercise1.WorkoutCompletionLogs.Single(l => l.Notes == "Done");
        var exercise1Dto = day.Exercises.Single(e => e.TrainingPlanExerciseId == exercise1.TrainingPlanExerciseId);
        Assert.True(exercise1Dto.IsCompleted);
        Assert.Equal(completedLog.WorkoutCompletionLogId, exercise1Dto.WorkoutCompletionLogId);
        Assert.Equal(completedLog.CreatedAt, exercise1Dto.CompletedAt);
        Assert.Equal("Done", exercise1Dto.TraineeNotes);
        Assert.Equal("COMPLETED", exercise1Dto.Status);

        var exercise2 = plan.TrainingPlanExercises.Single(e => e.ExerciseName == "Exercise 2");
        var latestLog = exercise2.WorkoutCompletionLogs.Single(l => l.Notes == "Latest");
        var exercise2Dto = day.Exercises.Single(e => e.TrainingPlanExerciseId == exercise2.TrainingPlanExerciseId);
        Assert.False(exercise2Dto.IsCompleted);
        Assert.Equal(latestLog.WorkoutCompletionLogId, exercise2Dto.WorkoutCompletionLogId);
        Assert.Equal(latestLog.CreatedAt, exercise2Dto.CompletedAt);
        Assert.Equal("Latest", exercise2Dto.TraineeNotes);
        Assert.Equal("INCOMPLETE", exercise2Dto.Status);
    }

    private static void AssertPastDayBackfill(
        TraineeWorkoutPlanProgressDto result,
        TrainingPlan plan,
        List<WorkoutCompletionLog> addedLogs,
        Guid traineeId,
        Guid orderDetailsId)
    {
        Assert.Equal(orderDetailsId, result.OrderDetailsId);
        AssertProgress(result, 5, 0, 0);

        var week = Assert.Single(result.Weeks);
        Assert.Equal(5, week.Days.Count);

        var past1 = plan.TrainingPlanExercises.Single(e => e.ExerciseName == "Past 1");
        var past2 = plan.TrainingPlanExercises.Single(e => e.ExerciseName == "Past 2");
        var past3 = plan.TrainingPlanExercises.Single(e => e.ExerciseName == "Past 3 Logged");
        var existingLog = past3.WorkoutCompletionLogs.Single();

        Assert.Equal(2, addedLogs.Count);
        var added1 = addedLogs.Single(l => l.TrainingPlanExerciseId == past1.TrainingPlanExerciseId);
        var added2 = addedLogs.Single(l => l.TrainingPlanExerciseId == past2.TrainingPlanExerciseId);
        Assert.Equal(traineeId, added1.TraineeId);
        Assert.Equal("INCOMPLETE", added1.Status);
        Assert.Equal(plan.StartDate!.Value.Date, added1.LogDate.Date);
        Assert.NotNull(added1.CreatedAt);
        Assert.Equal(traineeId, added2.TraineeId);
        Assert.Equal("INCOMPLETE", added2.Status);
        Assert.Equal(plan.StartDate!.Value.Date.AddDays(1), added2.LogDate.Date);
        Assert.NotNull(added2.CreatedAt);

        Assert.Equal(plan.StartDate!.Value.Date, week.Days[0].ScheduledDate!.Value.Date);
        Assert.Equal(plan.StartDate!.Value.Date.AddDays(1), week.Days[1].ScheduledDate!.Value.Date);
        Assert.Equal(plan.StartDate!.Value.Date.AddDays(2), week.Days[2].ScheduledDate!.Value.Date);
        Assert.Equal(plan.StartDate!.Value.Date.AddDays(3), week.Days[3].ScheduledDate!.Value.Date);
        Assert.Equal(plan.StartDate!.Value.Date.AddDays(4), week.Days[4].ScheduledDate!.Value.Date);
        Assert.False(week.Days[0].IsToday);
        Assert.True(week.Days[3].IsToday);
        Assert.False(week.Days[4].IsToday);

        var loggedDto = week.Days[2].Exercises.Single();
        Assert.Equal(existingLog.WorkoutCompletionLogId, loggedDto.WorkoutCompletionLogId);
        Assert.False(loggedDto.IsCompleted);
        Assert.Equal("INCOMPLETE", loggedDto.Status);
    }

    private static void AssertTodayFutureSchedule(
        TraineeWorkoutPlanProgressDto result,
        TrainingPlan plan,
        List<WorkoutCompletionLog> addedLogs)
    {
        AssertProgress(result, 3, 0, 0);
        Assert.Empty(addedLogs);

        var week = Assert.Single(result.Weeks);
        Assert.Equal(3, week.Days.Count);
        Assert.Equal(new short[] { 1, 2, 3 }, week.Days.Select(d => d.DayNumber));
        Assert.Equal(plan.StartDate!.Value.Date, week.Days[0].ScheduledDate!.Value.Date);
        Assert.Equal(plan.StartDate!.Value.Date.AddDays(1), week.Days[1].ScheduledDate!.Value.Date);
        Assert.Equal(plan.StartDate!.Value.Date.AddDays(2), week.Days[2].ScheduledDate!.Value.Date);
        Assert.True(week.Days[0].IsToday);
        Assert.False(week.Days[1].IsToday);
        Assert.False(week.Days[2].IsToday);
        Assert.Equal(3, result.TotalExercisesCount);
    }

    private static void AssertPendingValidOrder(
        TraineeWorkoutPlanProgressDto result,
        Guid coachId,
        Guid orderDetailsId,
        Guid packageId)
    {
        Assert.Equal(Guid.Empty, result.TrainingPlanId);
        Assert.Equal(orderDetailsId, result.OrderDetailsId);
        Assert.Equal(packageId, result.PackageId);
        Assert.Equal("Gold Plan", result.PackageTitle);
        Assert.Null(result.PackageThumbnailUrl);
        Assert.Equal("Training Plan Not Ready", result.PlanTitle);
        Assert.Equal(
            "Your coach is currently creating your personalized training program. Please check back soon!",
            result.PlanDescription);
        Assert.Equal("PENDING_COACH", result.PlanStatus);
        Assert.Null(result.StartDate);
        Assert.Null(result.EndDate);
        Assert.Equal(coachId, result.CoachId);
        Assert.Equal("Coach Lan", result.CoachName);
        Assert.Null(result.CoachAvatarUrl);
        AssertProgress(result, 0, 0, 0);
        Assert.Empty(result.Weeks);
    }

    private static void AssertEmptyPlan(TraineeWorkoutPlanProgressDto result)
    {
        Assert.Equal("Empty Plan", result.PlanTitle);
        AssertProgress(result, 0, 0, 0);
        Assert.Empty(result.Weeks);
    }

    private static void AssertOutsidePlanRange(
        TraineeWorkoutPlanProgressDto result,
        TrainingPlan plan)
    {
        Assert.Equal(plan.StartDate, result.StartDate);
        Assert.Equal(plan.EndDate, result.EndDate);
        AssertProgress(result, 0, 0, 0);
        Assert.Empty(result.Weeks);
    }

    private static void AssertOtherTraineeLogsIgnored(TraineeWorkoutPlanProgressDto result)
    {
        AssertProgress(result, 1, 0, 0);
        var day = Assert.Single(Assert.Single(result.Weeks).Days);
        var exercise = Assert.Single(day.Exercises);
        Assert.False(exercise.IsCompleted);
        Assert.Null(exercise.WorkoutCompletionLogId);
        Assert.Null(exercise.CompletedAt);
        Assert.Null(exercise.TraineeNotes);
        Assert.Null(exercise.Status);
    }

    private static void AssertPendingFallbacks(
        TraineeWorkoutPlanProgressDto result,
        Guid orderDetailsId)
    {
        Assert.Equal(Guid.Empty, result.TrainingPlanId);
        Assert.Equal(orderDetailsId, result.OrderDetailsId);
        Assert.Equal(Guid.Empty, result.PackageId);
        Assert.Equal("Training Package", result.PackageTitle);
        Assert.Equal("Training Plan Not Ready", result.PlanTitle);
        Assert.Equal("PENDING_COACH", result.PlanStatus);
        Assert.Equal(Guid.Empty, result.CoachId);
        Assert.Equal("Your Coach", result.CoachName);
        AssertProgress(result, 0, 0, 0);
        Assert.Empty(result.Weeks);
    }

    private static void AssertProgress(
        TraineeWorkoutPlanProgressDto result,
        int total,
        int completed,
        double percentage)
    {
        Assert.Equal(total, result.TotalExercisesCount);
        Assert.Equal(completed, result.CompletedExercisesCount);
        Assert.Equal(percentage, result.ProgressPercentage, 1);
    }

    private static TrainingPlan? BuildPlan(
        string scenario,
        Guid traineeId,
        Guid otherUserId,
        Guid coachId,
        Guid orderDetailsId,
        Guid packageId,
        Guid planId)
    {
        var today = DateTime.UtcNow.Date;

        switch (scenario)
        {
            case "FULL_PLAN_MAPPING":
            {
                var plan = CreateBasePlan(
                    planId,
                    orderDetailsId,
                    packageId,
                    traineeId,
                    coachId,
                    startDate: null,
                    endDate: null);

                var video = new VideoTutorial
                {
                    VideoTutorialId = Guid.NewGuid(),
                    CoachId = coachId,
                    Title = "Deadlift Form",
                    Description = "Keep a neutral spine.",
                    VideoUrl = "https://cdn.example/deadlift.mp4",
                    CreatedAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Unspecified)
                };

                var deadlift = CreateExercise(
                    planId,
                    "Deadlift",
                    2,
                    1,
                    2,
                    video,
                    notes: "Heavy",
                    sets: 4,
                    reps: 6,
                    durationMinutes: 20,
                    restSeconds: 90);
                var pushUps = CreateExercise(planId, "Push Ups", 1, 2, 2);
                var warmUp = CreateExercise(
                    planId,
                    "Warm Up",
                    1,
                    1,
                    2,
                    logs: new[]
                    {
                        CreateLog(
                            traineeId,
                            "INCOMPLETE",
                            new DateTime(2026, 1, 3, 8, 0, 0, DateTimeKind.Unspecified),
                            "Skipped")
                    });
                var squat = CreateExercise(
                    planId,
                    "Squat",
                    1,
                    1,
                    1,
                    logs: new[]
                    {
                        CreateLog(
                            traineeId,
                            "COMPLETED",
                            new DateTime(2026, 1, 2, 8, 0, 0, DateTimeKind.Unspecified),
                            "Done")
                    },
                    notes: "Depth",
                    sets: 5,
                    reps: 5,
                    durationMinutes: 15,
                    restSeconds: 120);
                var bench = CreateExercise(planId, "Bench Press", 2, 1, 1);

                plan.TrainingPlanExercises = new List<TrainingPlanExercise>
                {
                    deadlift,
                    pushUps,
                    warmUp,
                    squat,
                    bench
                };
                return plan;
            }

            case "LOG_SELECTION":
            {
                var plan = CreateBasePlan(
                    planId,
                    orderDetailsId,
                    packageId,
                    traineeId,
                    coachId,
                    startDate: null,
                    endDate: null);

                var exercise1 = CreateExercise(
                    planId,
                    "Exercise 1",
                    1,
                    1,
                    1,
                    logs: new[]
                    {
                        CreateLog(
                            traineeId,
                            "COMPLETED",
                            new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Unspecified),
                            "Done"),
                        CreateLog(
                            traineeId,
                            "INCOMPLETE",
                            new DateTime(2026, 1, 3, 8, 0, 0, DateTimeKind.Unspecified),
                            "Later"),
                        CreateLog(
                            otherUserId,
                            "COMPLETED",
                            new DateTime(2026, 1, 4, 8, 0, 0, DateTimeKind.Unspecified),
                            "Other")
                    });
                var exercise2 = CreateExercise(
                    planId,
                    "Exercise 2",
                    1,
                    1,
                    2,
                    logs: new[]
                    {
                        CreateLog(
                            traineeId,
                            "INCOMPLETE",
                            new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Unspecified),
                            "Older"),
                        CreateLog(
                            traineeId,
                            "INCOMPLETE",
                            new DateTime(2026, 1, 3, 8, 0, 0, DateTimeKind.Unspecified),
                            "Latest")
                    });

                plan.TrainingPlanExercises = new List<TrainingPlanExercise> { exercise1, exercise2 };
                return plan;
            }

            case "PAST_DAY_BACKFILL":
            {
                var plan = CreateBasePlan(
                    planId,
                    orderDetailsId,
                    packageId,
                    traineeId,
                    coachId,
                    startDate: today.AddDays(-3),
                    endDate: today.AddDays(10));

                plan.TrainingPlanExercises = new List<TrainingPlanExercise>
                {
                    CreateExercise(planId, "Past 1", 1, 1, 1),
                    CreateExercise(planId, "Past 2", 1, 2, 1),
                    CreateExercise(
                        planId,
                        "Past 3 Logged",
                        1,
                        3,
                        1,
                        logs: new[]
                        {
                            CreateLog(
                                traineeId,
                                "INCOMPLETE",
                                today.AddDays(-1),
                                "Already logged")
                        }),
                    CreateExercise(planId, "Today", 1, 4, 1),
                    CreateExercise(planId, "Future", 1, 5, 1)
                };
                return plan;
            }

            case "TODAY_FUTURE_SCHEDULE":
            {
                var plan = CreateBasePlan(
                    planId,
                    orderDetailsId,
                    packageId,
                    traineeId,
                    coachId,
                    startDate: today,
                    endDate: today.AddDays(2));

                plan.TrainingPlanExercises = new List<TrainingPlanExercise>
                {
                    CreateExercise(planId, "Today", 1, 1, 1),
                    CreateExercise(planId, "Tomorrow", 1, 2, 1),
                    CreateExercise(planId, "In Two Days", 1, 3, 1),
                    CreateExercise(planId, "Outside Range", 1, 4, 1)
                };
                return plan;
            }

            case "EMPTY_PLAN":
            {
                var plan = CreateBasePlan(
                    planId,
                    orderDetailsId,
                    packageId,
                    traineeId,
                    coachId,
                    startDate: null,
                    endDate: null);
                plan.Title = "Empty Plan";
                return plan;
            }

            case "OUTSIDE_PLAN_RANGE":
            {
                var plan = CreateBasePlan(
                    planId,
                    orderDetailsId,
                    packageId,
                    traineeId,
                    coachId,
                    startDate: today,
                    endDate: today);
                plan.TrainingPlanExercises = new List<TrainingPlanExercise>
                {
                    CreateExercise(planId, "Outside Range", 1, 2, 1)
                };
                return plan;
            }

            case "OTHER_TRAINEE_LOGS_IGNORED":
            {
                var plan = CreateBasePlan(
                    planId,
                    orderDetailsId,
                    packageId,
                    traineeId,
                    coachId,
                    startDate: null,
                    endDate: null);
                plan.TrainingPlanExercises = new List<TrainingPlanExercise>
                {
                    CreateExercise(
                        planId,
                        "Other Trainee Exercise",
                        1,
                        1,
                        1,
                        logs: new[]
                        {
                            CreateLog(
                                otherUserId,
                                "COMPLETED",
                                new DateTime(2026, 1, 3, 8, 0, 0, DateTimeKind.Unspecified),
                                "Other")
                        })
                };
                return plan;
            }

            case "BACKFILL_ADD_FAILURE":
            case "BACKFILL_SAVE_FAILURE":
            {
                var plan = CreateBasePlan(
                    planId,
                    orderDetailsId,
                    packageId,
                    traineeId,
                    coachId,
                    startDate: today.AddDays(-1),
                    endDate: today);
                plan.TrainingPlanExercises = new List<TrainingPlanExercise>
                {
                    CreateExercise(planId, "Past Missing Log", 1, 1, 1)
                };
                return plan;
            }

            case "INVALID_TRAINEE":
            case "ORDER_DETAIL_NOT_FOUND":
            case "OTHER_BUYER":
            case "ORDER_NAVIGATION_NULL":
            case "PLAN_QUERY_FAILURE":
            case "PENDING_VALID_ORDER":
            case "PENDING_FALLBACKS":
                return null;

            default:
                throw new InvalidOperationException($"Unsupported plan scenario \"{scenario}\".");
        }
    }

    private static OrderDetail? BuildOrderDetail(
        string scenario,
        Guid traineeId,
        Guid otherUserId,
        Guid coachId,
        Guid orderDetailsId,
        Guid packageId)
    {
        var orderId = Guid.NewGuid();

        switch (scenario)
        {
            case "PENDING_VALID_ORDER":
                return new OrderDetail
                {
                    OrderDetailsId = orderDetailsId,
                    OrderId = orderId,
                    PackageId = packageId,
                    PackageTitle = "Gold Plan",
                    CoachName = "Coach Lan",
                    Order = new Order
                    {
                        OrderId = orderId,
                        BuyerId = traineeId,
                        CoachId = coachId,
                        OrderStatus = "ACTIVE",
                        CreatedAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Unspecified)
                    }
                };

            case "PENDING_FALLBACKS":
                return new OrderDetail
                {
                    OrderDetailsId = orderDetailsId,
                    OrderId = orderId,
                    PackageId = null,
                    PackageTitle = null,
                    CoachName = null,
                    Package = null,
                    Order = new Order
                    {
                        OrderId = orderId,
                        BuyerId = traineeId,
                        CoachId = null,
                        OrderStatus = "ACTIVE",
                        CreatedAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Unspecified)
                    }
                };

            case "OTHER_BUYER":
                return new OrderDetail
                {
                    OrderDetailsId = orderDetailsId,
                    OrderId = orderId,
                    PackageId = packageId,
                    PackageTitle = "Gold Plan",
                    Order = new Order
                    {
                        OrderId = orderId,
                        BuyerId = otherUserId,
                        CoachId = coachId,
                        OrderStatus = "ACTIVE"
                    }
                };

            case "ORDER_NAVIGATION_NULL":
                return new OrderDetail
                {
                    OrderDetailsId = orderDetailsId,
                    OrderId = orderId,
                    PackageId = packageId,
                    PackageTitle = "Gold Plan",
                    Order = null!
                };

            case "ORDER_DETAIL_NOT_FOUND":
                return null;

            default:
                return null;
        }
    }

    private static TrainingPlan CreateBasePlan(
        Guid planId,
        Guid orderDetailsId,
        Guid packageId,
        Guid traineeId,
        Guid coachId,
        DateTime? startDate,
        DateTime? endDate)
    {
        var orderId = Guid.NewGuid();
        var package = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = "Strength Builder",
            Media = new List<TrainingPackageMedium>
            {
                new()
                {
                    MediaId = Guid.NewGuid(),
                    PackageId = packageId,
                    MediaUrl = "https://cdn.example/second.jpg",
                    MediaType = "IMAGE",
                    SortOrder = 2
                },
                new()
                {
                    MediaId = Guid.NewGuid(),
                    PackageId = packageId,
                    MediaUrl = "https://cdn.example/cover.jpg",
                    MediaType = "IMAGE",
                    SortOrder = 1
                }
            }
        };

        var order = new Order
        {
            OrderId = orderId,
            BuyerId = traineeId,
            CoachId = coachId,
            OrderStatus = "ACTIVE",
            CreatedAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Unspecified)
        };

        var orderDetails = new OrderDetail
        {
            OrderDetailsId = orderDetailsId,
            OrderId = orderId,
            PackageId = packageId,
            PackageTitle = "Strength Builder",
            Package = package,
            Order = order,
            CreatedAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Unspecified)
        };

        return new TrainingPlan
        {
            TrainingPlanId = planId,
            CoachId = coachId,
            OrderDetailsId = orderDetailsId,
            OrderDetails = orderDetails,
            Title = "12-Week Strength",
            Description = "Build strength",
            Status = "PUBLISHED",
            StartDate = startDate,
            EndDate = endDate,
            CreatedAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Unspecified),
            Coach = new CoachProfile
            {
                CoachId = coachId,
                Coach = new User
                {
                    UserId = coachId,
                    FullName = "Coach Minh",
                    AvatarUrl = "https://cdn.example/avatar.jpg"
                }
            },
            TrainingPlanExercises = new List<TrainingPlanExercise>()
        };
    }

    private static TrainingPlanExercise CreateExercise(
        Guid planId,
        string? exerciseName,
        short? weekNumber,
        short? dayNumber,
        short? sortOrder,
        VideoTutorial? videoTutorial = null,
        IEnumerable<WorkoutCompletionLog>? logs = null,
        string? notes = null,
        short? sets = 3,
        short? reps = 12,
        short? durationMinutes = 20,
        short? restSeconds = 60)
    {
        var exerciseId = Guid.NewGuid();
        var exercise = new TrainingPlanExercise
        {
            TrainingPlanExerciseId = exerciseId,
            TrainingPlanId = planId,
            ExerciseName = exerciseName,
            WeekNumber = weekNumber,
            DayNumber = dayNumber,
            SortOrder = sortOrder,
            Sets = sets,
            Reps = reps,
            DurationMinutes = durationMinutes,
            RestSeconds = restSeconds,
            Notes = notes,
            VideoTutorial = videoTutorial,
            VideoTutorialId = videoTutorial?.VideoTutorialId,
            WorkoutCompletionLogs = new List<WorkoutCompletionLog>()
        };

        if (logs != null)
        {
            foreach (var log in logs)
            {
                log.TrainingPlanExerciseId = exerciseId;
                exercise.WorkoutCompletionLogs.Add(log);
            }
        }

        return exercise;
    }

    private static WorkoutCompletionLog CreateLog(
        Guid traineeId,
        string status,
        DateTime createdAt,
        string? notes)
    {
        return new WorkoutCompletionLog
        {
            WorkoutCompletionLogId = Guid.NewGuid(),
            TraineeId = traineeId,
            LogDate = createdAt.Date,
            Status = status,
            Notes = notes,
            CreatedAt = createdAt
        };
    }

    private static async Task AssertExpectedException(
        string expectedType,
        string expectedMessage,
        Func<Task> action)
    {
        switch (expectedType.Trim().ToUpperInvariant())
        {
            case "FORBIDDEN":
            {
                var exception = await Assert.ThrowsAsync<ForbiddenException>(action);
                Assert.Equal(expectedMessage, exception.Message);
                break;
            }

            case "NOT_FOUND":
            {
                var exception = await Assert.ThrowsAsync<NotFoundException>(action);
                Assert.Equal(expectedMessage, exception.Message);
                break;
            }

            case "INVALID_OPERATION":
            {
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(action);
                Assert.Equal(expectedMessage, exception.Message);
                break;
            }

            case "DB_UPDATE":
            {
                var exception = await Assert.ThrowsAsync<DbUpdateException>(action);
                Assert.Equal(expectedMessage, exception.Message);
                break;
            }

            default:
                throw new InvalidOperationException($"Unsupported expected exception type \"{expectedType}\".");
        }
    }

    public class ViewTrainingProgressTestCase
    {
        public string UtcId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Scenario { get; set; } = string.Empty;
        public string ExpectedExceptionType { get; set; } = string.Empty;
        public string ExpectedMessage { get; set; } = string.Empty;

        public override string ToString() => UtcId;
    }
}
