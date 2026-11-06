using System;
using System.Collections.Generic;
using System.Globalization;
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

public class MarkDailyWorkoutServiceTests
{
    private readonly Mock<ITrainingPlanRepository> _trainingPlanRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private WorkoutCompletionLog? _addedLog;
    private WorkoutCompletionLog? _removedLog;

    public static TheoryData<MarkDailyWorkoutTestCase> TestCases
    {
        get
        {
            var data = new TheoryData<MarkDailyWorkoutTestCase>();
            var assembly = typeof(MarkDailyWorkoutServiceTests).Assembly;
            using var stream = assembly.GetManifestResourceStream(
                "FitSocial.UnitTests.TestData.MarkDailyWorkoutTestCases.csv");
            if (stream == null) return data;
            using var reader = new StreamReader(stream);

            var rows = CsvTestHelper.ParseCsv(reader.ReadToEnd());
            if (rows.Count <= 1) return data;

            var header = rows[0];
            var index = header.Select((name, i) => (name.Trim(), i)).ToDictionary(x => x.Item1, x => x.Item2);

            foreach (var row in rows.Skip(1))
            {
                if (row.All(string.IsNullOrWhiteSpace)) continue;

                data.Add(new MarkDailyWorkoutTestCase
                {
                    UtcId = CsvTestHelper.Get(row, index, "UTCID"),
                    Type = CsvTestHelper.Get(row, index, "Type"),
                    Scenario = CsvTestHelper.Get(row, index, "Scenario"),
                    BuyerMode = CsvTestHelper.Get(row, index, "BuyerMode"),
                    ExistingLogStatus = CsvTestHelper.Get(row, index, "ExistingLogStatus"),
                    ExistingLogNotes = CsvTestHelper.Get(row, index, "ExistingLogNotes"),
                    DtoStatus = CsvTestHelper.Get(row, index, "DtoStatus"),
                    DtoNotes = CsvTestHelper.Get(row, index, "DtoNotes"),
                    DtoLogDate = CsvTestHelper.Get(row, index, "DtoLogDate"),
                    PlanMode = CsvTestHelper.Get(row, index, "PlanMode"),
                    SaveChangesThrows = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "SaveChangesThrows")),
                    ExpectedExceptionType = CsvTestHelper.Get(row, index, "ExpectedExceptionType"),
                    ExpectedMessage = CsvTestHelper.Get(row, index, "ExpectedMessage")
                });
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public async Task MarkDailyWorkoutTest(MarkDailyWorkoutTestCase tc)
    {
        _trainingPlanRepository.Reset();
        _unitOfWork.Reset();
        _addedLog = null;
        _removedLog = null;

        var traineeId = string.Equals(tc.Scenario, "INVALID_TRAINEE", StringComparison.OrdinalIgnoreCase)
            ? Guid.Empty
            : Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var exerciseId = string.Equals(tc.Scenario, "EMPTY_EXERCISE_ID", StringComparison.OrdinalIgnoreCase)
            ? Guid.Empty
            : Guid.NewGuid();
        var buyerId = string.Equals(tc.BuyerMode, "OTHER", StringComparison.OrdinalIgnoreCase)
            ? otherUserId
            : traineeId;

        var exerciseContext = CreateExercise(
            exerciseId,
            traineeId,
            buyerId,
            ParseOptional(tc.ExistingLogStatus, "NONE"),
            ParseOptional(tc.ExistingLogNotes, "NONE"));

        var expectedLogDate = ParseDate(tc.DtoLogDate);
        var dto = string.Equals(tc.Scenario, "NULL_DTO", StringComparison.OrdinalIgnoreCase)
            ? null
            : new ToggleWorkoutCompletionDto
            {
                TrainingPlanExerciseId = exerciseId,
                Status = ParseStatus(tc.DtoStatus),
                Notes = ParseNotes(tc.DtoNotes),
                LogDate = expectedLogDate
            };

        SetupRepository(tc, exerciseContext, traineeId, otherUserId);
        var sut = CreateSut();

        if (!string.Equals(tc.ExpectedExceptionType, "NONE", StringComparison.OrdinalIgnoreCase))
        {
            await AssertExpectedException(
                tc.ExpectedExceptionType,
                tc.ExpectedMessage,
                () => sut.ToggleWorkoutCompletionAsync(traineeId, dto!));

            VerifyFailureCalls(tc.Scenario);
            return;
        }

        var result = await sut.ToggleWorkoutCompletionAsync(traineeId, dto!);

        VerifySuccessCalls(tc.Scenario);
        Assert.Equal(exerciseId, result.TrainingPlanExerciseId);

        switch (tc.Scenario.Trim().ToUpperInvariant())
        {
            case "NEW_COMPLETE_DEFAULT":
            {
                var log = Assert.IsType<WorkoutCompletionLog>(_addedLog);
                Assert.Equal(traineeId, log.TraineeId);
                Assert.Equal(exerciseId, log.TrainingPlanExerciseId);
                Assert.Equal("COMPLETED", log.Status);
                Assert.Null(log.Notes);
                Assert.NotNull(log.CreatedAt);
                Assert.InRange(log.LogDate, DateTime.UtcNow.AddMinutes(-2), DateTime.UtcNow.AddMinutes(2));
                Assert.Equal(log.WorkoutCompletionLogId, result.WorkoutCompletionLogId);
                Assert.Equal(log.CreatedAt, result.CompletedAt);
                Assert.True(result.IsCompleted);
                Assert.Equal("COMPLETED", result.Status);
                Assert.Null(result.Notes);
                AssertProgress(result, 3, 1, 33.3);
                break;
            }

            case "NEW_COMPLETE_WITH_DETAILS":
            {
                var log = Assert.IsType<WorkoutCompletionLog>(_addedLog);
                Assert.Equal("COMPLETED", log.Status);
                Assert.Equal("Felt strong", log.Notes);
                Assert.Equal(expectedLogDate, log.LogDate);
                Assert.NotNull(log.CreatedAt);
                Assert.Equal(log.WorkoutCompletionLogId, result.WorkoutCompletionLogId);
                Assert.Equal(log.CreatedAt, result.CompletedAt);
                Assert.True(result.IsCompleted);
                Assert.Equal("COMPLETED", result.Status);
                Assert.Equal("Felt strong", result.Notes);
                AssertProgress(result, 2, 1, 50.0);
                break;
            }

            case "UPDATE_COMPLETED_NOTES":
            {
                var log = Assert.IsType<WorkoutCompletionLog>(exerciseContext.ExistingLog);
                Assert.Equal("COMPLETED", log.Status);
                Assert.Equal("Updated notes", log.Notes);
                Assert.Equal(log.WorkoutCompletionLogId, result.WorkoutCompletionLogId);
                Assert.Equal(log.CreatedAt, result.CompletedAt);
                Assert.True(result.IsCompleted);
                Assert.Equal("COMPLETED", result.Status);
                Assert.Equal("Updated notes", result.Notes);
                AssertProgress(result, 4, 2, 50.0);
                break;
            }

            case "MARK_INCOMPLETE_LOG_COMPLETE":
            {
                var log = Assert.IsType<WorkoutCompletionLog>(exerciseContext.ExistingLog);
                Assert.Equal("COMPLETED", log.Status);
                Assert.Equal("Old notes", log.Notes);
                Assert.Equal(log.WorkoutCompletionLogId, result.WorkoutCompletionLogId);
                Assert.Equal(log.CreatedAt, result.CompletedAt);
                Assert.True(result.IsCompleted);
                Assert.Equal("COMPLETED", result.Status);
                Assert.Equal("Old notes", result.Notes);
                AssertProgress(result, 2, 2, 100.0);
                break;
            }

            case "MARK_COMPLETED_INCOMPLETE":
            {
                var log = Assert.IsType<WorkoutCompletionLog>(exerciseContext.ExistingLog);
                Assert.Equal("INCOMPLETE", log.Status);
                Assert.Equal("Stopped early", log.Notes);
                Assert.Equal(log.WorkoutCompletionLogId, result.WorkoutCompletionLogId);
                Assert.Null(result.CompletedAt);
                Assert.False(result.IsCompleted);
                Assert.Equal("INCOMPLETE", result.Status);
                Assert.Equal("Stopped early", result.Notes);
                AssertProgress(result, 3, 1, 33.3);
                break;
            }

            case "TOGGLE_OFF_DELETE":
            {
                var log = Assert.IsType<WorkoutCompletionLog>(exerciseContext.ExistingLog);
                Assert.Same(log, _removedLog);
                Assert.Null(_addedLog);
                Assert.False(result.IsCompleted);
                Assert.Equal("INCOMPLETE", result.Status);
                Assert.Null(result.WorkoutCompletionLogId);
                Assert.Null(result.CompletedAt);
                Assert.Null(result.Notes);
                AssertProgress(result, 3, 0, 0);
                break;
            }

            case "TOGGLE_OFF_WHITESPACE":
            {
                var log = Assert.IsType<WorkoutCompletionLog>(exerciseContext.ExistingLog);
                Assert.Same(log, _removedLog);
                Assert.Null(_addedLog);
                Assert.False(result.IsCompleted);
                Assert.Equal("INCOMPLETE", result.Status);
                Assert.Null(result.WorkoutCompletionLogId);
                Assert.Null(result.CompletedAt);
                Assert.Equal("   ", result.Notes);
                AssertProgress(result, 3, 0, 0);
                break;
            }

            case "LOWERCASE_INCOMPLETE":
            {
                var log = Assert.IsType<WorkoutCompletionLog>(exerciseContext.ExistingLog);
                Assert.Equal("INCOMPLETE", log.Status);
                Assert.Equal("Taking a rest", log.Notes);
                Assert.Equal(log.WorkoutCompletionLogId, result.WorkoutCompletionLogId);
                Assert.Null(result.CompletedAt);
                Assert.False(result.IsCompleted);
                Assert.Equal("INCOMPLETE", result.Status);
                Assert.Equal("Taking a rest", result.Notes);
                AssertProgress(result, 3, 1, 33.3);
                break;
            }

            case "PLAN_NULL_PROGRESS":
            {
                var log = Assert.IsType<WorkoutCompletionLog>(exerciseContext.ExistingLog);
                Assert.Equal("COMPLETED", log.Status);
                Assert.Equal("Updated notes", log.Notes);
                Assert.Equal(log.WorkoutCompletionLogId, result.WorkoutCompletionLogId);
                Assert.Equal(log.CreatedAt, result.CompletedAt);
                Assert.True(result.IsCompleted);
                Assert.Equal("COMPLETED", result.Status);
                Assert.Equal("Updated notes", result.Notes);
                AssertProgress(result, 0, 0, 0);
                break;
            }

            case "MIXED_LOGS_PROGRESS":
            {
                var log = Assert.IsType<WorkoutCompletionLog>(exerciseContext.ExistingLog);
                Assert.Equal("COMPLETED", log.Status);
                Assert.Equal("Updated notes", log.Notes);
                Assert.Equal(log.WorkoutCompletionLogId, result.WorkoutCompletionLogId);
                Assert.Equal(log.CreatedAt, result.CompletedAt);
                Assert.True(result.IsCompleted);
                Assert.Equal("COMPLETED", result.Status);
                Assert.Equal("Updated notes", result.Notes);
                AssertProgress(result, 3, 1, 33.3);
                break;
            }

            default:
                throw new InvalidOperationException(
                    $"{tc.UtcId}: unsupported success scenario \"{tc.Scenario}\".");
        }
    }

    private TrainingPlanService CreateSut()
    {
        return new TrainingPlanService(_trainingPlanRepository.Object, _unitOfWork.Object);
    }

    private void SetupRepository(
        MarkDailyWorkoutTestCase tc,
        ExerciseContext exerciseContext,
        Guid traineeId,
        Guid otherUserId)
    {
        _trainingPlanRepository
            .Setup(r => r.GetExerciseByIdAsync(
                exerciseContext.Exercise.TrainingPlanExerciseId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Equals(tc.Scenario, "EXERCISE_NOT_FOUND", StringComparison.OrdinalIgnoreCase)
                ? null
                : exerciseContext.Exercise);

        _trainingPlanRepository
            .Setup(r => r.GetWorkoutLogAsync(
                exerciseContext.Exercise.TrainingPlanExerciseId,
                traineeId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(exerciseContext.ExistingLog);

        _trainingPlanRepository
            .Setup(r => r.AddWorkoutLogAsync(
                It.IsAny<WorkoutCompletionLog>(),
                It.IsAny<CancellationToken>()))
            .Callback<WorkoutCompletionLog, CancellationToken>((log, _) => _addedLog = log)
            .Returns(Task.CompletedTask);

        _trainingPlanRepository
            .Setup(r => r.RemoveWorkoutLog(It.IsAny<WorkoutCompletionLog>()))
            .Callback<WorkoutCompletionLog>(log => _removedLog = log);

        var saveSetup = _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()));
        if (tc.SaveChangesThrows)
        {
            saveSetup.ThrowsAsync(new DbUpdateException(tc.ExpectedMessage));
        }
        else
        {
            saveSetup.ReturnsAsync(1);
        }

        var plan = BuildPlan(tc.PlanMode, traineeId, otherUserId, exerciseContext.OrderDetailsId);
        _trainingPlanRepository
            .Setup(r => r.GetTraineePlanWithLogsAsync(
                exerciseContext.OrderDetailsId,
                traineeId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
    }

    private void VerifyFailureCalls(string scenario)
    {
        switch (scenario.Trim().ToUpperInvariant())
        {
            case "INVALID_TRAINEE":
            case "NULL_DTO":
            case "EMPTY_EXERCISE_ID":
                _trainingPlanRepository.Verify(
                    r => r.GetExerciseByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                    Times.Never);
                _trainingPlanRepository.Verify(
                    r => r.GetWorkoutLogAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                    Times.Never);
                _trainingPlanRepository.Verify(
                    r => r.AddWorkoutLogAsync(It.IsAny<WorkoutCompletionLog>(), It.IsAny<CancellationToken>()),
                    Times.Never);
                _trainingPlanRepository.Verify(
                    r => r.RemoveWorkoutLog(It.IsAny<WorkoutCompletionLog>()),
                    Times.Never);
                _trainingPlanRepository.Verify(
                    r => r.GetTraineePlanWithLogsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                    Times.Never);
                _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
                break;

            case "EXERCISE_NOT_FOUND":
                _trainingPlanRepository.Verify(
                    r => r.GetExerciseByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                    Times.Once);
                _trainingPlanRepository.Verify(
                    r => r.GetWorkoutLogAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                    Times.Never);
                _trainingPlanRepository.Verify(
                    r => r.AddWorkoutLogAsync(It.IsAny<WorkoutCompletionLog>(), It.IsAny<CancellationToken>()),
                    Times.Never);
                _trainingPlanRepository.Verify(
                    r => r.RemoveWorkoutLog(It.IsAny<WorkoutCompletionLog>()),
                    Times.Never);
                _trainingPlanRepository.Verify(
                    r => r.GetTraineePlanWithLogsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                    Times.Never);
                _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
                break;

            case "OTHER_BUYER":
                _trainingPlanRepository.Verify(
                    r => r.GetExerciseByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                    Times.Once);
                _trainingPlanRepository.Verify(
                    r => r.GetWorkoutLogAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                    Times.Never);
                _trainingPlanRepository.Verify(
                    r => r.AddWorkoutLogAsync(It.IsAny<WorkoutCompletionLog>(), It.IsAny<CancellationToken>()),
                    Times.Never);
                _trainingPlanRepository.Verify(
                    r => r.RemoveWorkoutLog(It.IsAny<WorkoutCompletionLog>()),
                    Times.Never);
                _trainingPlanRepository.Verify(
                    r => r.GetTraineePlanWithLogsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                    Times.Never);
                _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
                break;

            case "SAVE_FAILURE":
                _trainingPlanRepository.Verify(
                    r => r.GetExerciseByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                    Times.Once);
                _trainingPlanRepository.Verify(
                    r => r.GetWorkoutLogAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                    Times.Once);
                _trainingPlanRepository.Verify(
                    r => r.AddWorkoutLogAsync(It.IsAny<WorkoutCompletionLog>(), It.IsAny<CancellationToken>()),
                    Times.Once);
                _trainingPlanRepository.Verify(
                    r => r.RemoveWorkoutLog(It.IsAny<WorkoutCompletionLog>()),
                    Times.Never);
                _trainingPlanRepository.Verify(
                    r => r.GetTraineePlanWithLogsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                    Times.Never);
                _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
                break;

            default:
                throw new InvalidOperationException($"Unsupported failure scenario \"{scenario}\".");
        }

        _trainingPlanRepository.VerifyNoOtherCalls();
        _unitOfWork.VerifyNoOtherCalls();
    }

    private void VerifySuccessCalls(string scenario)
    {
        _trainingPlanRepository.Verify(
            r => r.GetExerciseByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _trainingPlanRepository.Verify(
            r => r.GetWorkoutLogAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _trainingPlanRepository.Verify(
            r => r.GetTraineePlanWithLogsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        switch (scenario.Trim().ToUpperInvariant())
        {
            case "NEW_COMPLETE_DEFAULT":
            case "NEW_COMPLETE_WITH_DETAILS":
                _trainingPlanRepository.Verify(
                    r => r.AddWorkoutLogAsync(It.IsAny<WorkoutCompletionLog>(), It.IsAny<CancellationToken>()),
                    Times.Once);
                _trainingPlanRepository.Verify(
                    r => r.RemoveWorkoutLog(It.IsAny<WorkoutCompletionLog>()),
                    Times.Never);
                break;

            case "TOGGLE_OFF_DELETE":
            case "TOGGLE_OFF_WHITESPACE":
                _trainingPlanRepository.Verify(
                    r => r.AddWorkoutLogAsync(It.IsAny<WorkoutCompletionLog>(), It.IsAny<CancellationToken>()),
                    Times.Never);
                _trainingPlanRepository.Verify(
                    r => r.RemoveWorkoutLog(It.IsAny<WorkoutCompletionLog>()),
                    Times.Once);
                break;

            case "UPDATE_COMPLETED_NOTES":
            case "MARK_INCOMPLETE_LOG_COMPLETE":
            case "MARK_COMPLETED_INCOMPLETE":
            case "LOWERCASE_INCOMPLETE":
            case "PLAN_NULL_PROGRESS":
            case "MIXED_LOGS_PROGRESS":
                _trainingPlanRepository.Verify(
                    r => r.AddWorkoutLogAsync(It.IsAny<WorkoutCompletionLog>(), It.IsAny<CancellationToken>()),
                    Times.Never);
                _trainingPlanRepository.Verify(
                    r => r.RemoveWorkoutLog(It.IsAny<WorkoutCompletionLog>()),
                    Times.Never);
                break;

            default:
                throw new InvalidOperationException($"Unsupported success scenario \"{scenario}\".");
        }

        _trainingPlanRepository.VerifyNoOtherCalls();
        _unitOfWork.VerifyNoOtherCalls();
    }

    private static async Task AssertExpectedException(string expectedType, string expectedMessage, Func<Task> action)
    {
        switch (expectedType.Trim().ToUpperInvariant())
        {
            case "FORBIDDEN":
            {
                var exception = await Assert.ThrowsAsync<ForbiddenException>(action);
                Assert.Equal(expectedMessage, exception.Message);
                break;
            }

            case "VALIDATION":
            {
                var exception = await Assert.ThrowsAsync<ValidationException>(action);
                Assert.Equal(expectedMessage, exception.Message);
                break;
            }

            case "NOT_FOUND":
            {
                var exception = await Assert.ThrowsAsync<NotFoundException>(action);
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

    private static void AssertProgress(WorkoutCompletionResultDto result, int total, int completed, double percentage)
    {
        Assert.Equal(total, result.TotalExercisesCount);
        Assert.Equal(completed, result.CompletedExercisesCount);
        Assert.Equal(percentage, result.ProgressPercentage, 1);
    }

    private static ExerciseContext CreateExercise(
        Guid exerciseId,
        Guid traineeId,
        Guid buyerId,
        string? existingLogStatus,
        string? existingLogNotes)
    {
        var orderDetailsId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        var order = new Order
        {
            OrderId = orderId,
            BuyerId = buyerId,
            OrderStatus = "ACTIVE",
            CreatedAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Unspecified)
        };

        var orderDetails = new OrderDetail
        {
            OrderDetailsId = orderDetailsId,
            OrderId = orderId,
            Order = order,
            PackageType = "BOTH",
            CreatedAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Unspecified)
        };

        var plan = new TrainingPlan
        {
            TrainingPlanId = planId,
            OrderDetailsId = orderDetailsId,
            OrderDetails = orderDetails,
            Title = "Daily Workout Plan",
            Status = "PUBLISHED",
            CreatedAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Unspecified)
        };

        var exercise = new TrainingPlanExercise
        {
            TrainingPlanExerciseId = exerciseId,
            TrainingPlanId = planId,
            ExerciseName = "Push Ups",
            WeekNumber = 1,
            DayNumber = 1,
            TrainingPlan = plan,
            WorkoutCompletionLogs = new List<WorkoutCompletionLog>()
        };

        WorkoutCompletionLog? existingLog = null;
        if (existingLogStatus != null)
        {
            existingLog = new WorkoutCompletionLog
            {
                WorkoutCompletionLogId = Guid.NewGuid(),
                TraineeId = traineeId,
                TrainingPlanExerciseId = exerciseId,
                LogDate = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Unspecified),
                Status = existingLogStatus,
                Notes = existingLogNotes,
                CreatedAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Unspecified)
            };

            exercise.WorkoutCompletionLogs.Add(existingLog);
        }

        return new ExerciseContext(exercise, orderDetailsId, existingLog);
    }

    private static TrainingPlan? BuildPlan(string mode, Guid traineeId, Guid otherUserId, Guid orderDetailsId)
    {
        var normalized = mode.Trim().ToUpperInvariant();
        if (normalized == "NULL") return null;

        var plan = new TrainingPlan
        {
            TrainingPlanId = Guid.NewGuid(),
            OrderDetailsId = orderDetailsId,
            Title = "Refreshed Workout Plan",
            Status = "PUBLISHED",
            CreatedAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Unspecified),
            TrainingPlanExercises = new List<TrainingPlanExercise>()
        };

        void AddExercise(params (Guid TraineeId, string Status)[] logs)
        {
            var exerciseId = Guid.NewGuid();
            var exercise = new TrainingPlanExercise
            {
                TrainingPlanExerciseId = exerciseId,
                TrainingPlanId = plan.TrainingPlanId,
                ExerciseName = $"Exercise {plan.TrainingPlanExercises.Count + 1}",
                WeekNumber = 1,
                DayNumber = 1,
                WorkoutCompletionLogs = logs.Select(log => new WorkoutCompletionLog
                {
                    WorkoutCompletionLogId = Guid.NewGuid(),
                    TraineeId = log.TraineeId,
                    TrainingPlanExerciseId = exerciseId,
                    LogDate = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Unspecified),
                    Status = log.Status,
                    CreatedAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Unspecified)
                }).ToList()
            };

            plan.TrainingPlanExercises.Add(exercise);
        }

        switch (normalized)
        {
            case "THREE_ONE_COMPLETED":
                AddExercise((traineeId, "COMPLETED"));
                AddExercise();
                AddExercise();
                break;

            case "TWO_ONE_COMPLETED":
                AddExercise((traineeId, "COMPLETED"));
                AddExercise();
                break;

            case "FOUR_TWO_COMPLETED":
                AddExercise((traineeId, "COMPLETED"));
                AddExercise((traineeId, "COMPLETED"));
                AddExercise();
                AddExercise();
                break;

            case "TWO_TWO_COMPLETED":
                AddExercise((traineeId, "COMPLETED"));
                AddExercise((traineeId, "COMPLETED"));
                break;

            case "THREE_ZERO_COMPLETED":
                AddExercise();
                AddExercise();
                AddExercise();
                break;

            case "MIXED_LOGS":
                AddExercise((traineeId, "COMPLETED"));
                AddExercise((otherUserId, "COMPLETED"));
                AddExercise((traineeId, "INCOMPLETE"));
                break;

            default:
                throw new InvalidOperationException($"Unsupported plan mode \"{mode}\".");
        }

        return plan;
    }

    private static string? ParseOptional(string value, string noneToken)
    {
        return string.Equals(value.Trim(), noneToken, StringComparison.OrdinalIgnoreCase)
            ? null
            : value;
    }

    private static string? ParseStatus(string value)
    {
        return value.Trim().ToUpperInvariant() switch
        {
            "NULL" => null,
            "SPACES" => "   ",
            "LOWERCASE_INCOMPLETE" => "incomplete",
            _ => value
        };
    }

    private static string? ParseNotes(string value)
    {
        return value.Trim().ToUpperInvariant() switch
        {
            "NULL" => null,
            "SPACES" => "   ",
            _ => value
        };
    }

    private static DateTime? ParseDate(string value)
    {
        if (string.Equals(value.Trim(), "NULL", StringComparison.OrdinalIgnoreCase)) return null;

        return DateTime.SpecifyKind(
            DateTime.ParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeKind.Unspecified);
    }

    private sealed record ExerciseContext(
        TrainingPlanExercise Exercise,
        Guid OrderDetailsId,
        WorkoutCompletionLog? ExistingLog);

    public class MarkDailyWorkoutTestCase
    {
        public string UtcId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Scenario { get; set; } = string.Empty;
        public string BuyerMode { get; set; } = string.Empty;
        public string ExistingLogStatus { get; set; } = string.Empty;
        public string ExistingLogNotes { get; set; } = string.Empty;
        public string DtoStatus { get; set; } = string.Empty;
        public string DtoNotes { get; set; } = string.Empty;
        public string DtoLogDate { get; set; } = string.Empty;
        public string PlanMode { get; set; } = string.Empty;
        public bool SaveChangesThrows { get; set; }
        public string ExpectedExceptionType { get; set; } = string.Empty;
        public string ExpectedMessage { get; set; } = string.Empty;
        public override string ToString() => UtcId;
    }
}
