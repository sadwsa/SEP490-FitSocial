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

public class CoachTrainingPlanServiceTests
{
    private readonly Mock<ITrainingPlanRepository> _mockPlanRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly TrainingPlanService _service;

    public CoachTrainingPlanServiceTests()
    {
        _mockPlanRepo = new Mock<ITrainingPlanRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _service = new TrainingPlanService(_mockPlanRepo.Object, _mockUow.Object);
    }

    [Fact]
    public async Task CreateTrainingPlanAsync_ValidPlanWithVideoTutorial_ShouldPersistPlanAndReturnDetails()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var orderDetailsId = Guid.NewGuid();
        var existingVideoId = Guid.NewGuid();

        var orderDetail = new OrderDetail
        {
            OrderDetailsId = orderDetailsId,
            Order = new Order
            {
                OrderId = Guid.NewGuid(),
                CoachId = coachId
            }
        };

        _mockPlanRepo
            .Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(orderDetailsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderDetail);

        TrainingPlan? savedPlan = null;
        _mockPlanRepo
            .Setup(r => r.AddAsync(It.IsAny<TrainingPlan>(), It.IsAny<CancellationToken>()))
            .Callback<TrainingPlan, CancellationToken>((p, _) => savedPlan = p)
            .Returns(Task.CompletedTask);

        _mockPlanRepo
            .Setup(r => r.AddVideoTutorialAsync(It.IsAny<VideoTutorial>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUow
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var dto = new CreateTrainingPlanDto
        {
            OrderDetailsId = orderDetailsId,
            Title = "Kế hoạch Tăng cơ 4 Tuần",
            Description = "Lộ trình tập luyện chuyên sâu",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(28),
            Status = "ACTIVE",
            Exercises = new List<TrainingPlanExerciseDto>
            {
                new()
                {
                    ExerciseName = "Barbell Bench Press",
                    WeekNumber = 1,
                    DayNumber = 1,
                    Sets = 4,
                    Reps = 10,
                    DurationMinutes = 15,
                    RestSeconds = 90,
                    SortOrder = 1,
                    VideoTutorialId = existingVideoId
                },
                new()
                {
                    ExerciseName = "Incline Dumbbell Fly",
                    WeekNumber = 1,
                    DayNumber = 1,
                    Sets = 3,
                    Reps = 12,
                    DurationMinutes = 12,
                    RestSeconds = 60,
                    SortOrder = 2,
                    NewVideoTutorial = new CreateVideoTutorialDto
                    {
                        Title = "Hướng dẫn Incline Fly chuẩn form",
                        Description = "Tránh chấn thương vai",
                        VideoUrl = "https://cdn.fitsocial.com/videos/incline-fly.mp4"
                    }
                }
            }
        };

        // Act
        var result = await _service.CreateTrainingPlanAsync(coachId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(coachId, result.CoachId);
        Assert.Equal(orderDetailsId, result.OrderDetailsId);
        Assert.Equal("Kế hoạch Tăng cơ 4 Tuần", result.Title);
        Assert.Equal(2, result.Exercises.Count);

        // Verify Video Tutorial creation was triggered for exercise 2
        _mockPlanRepo.Verify(r => r.AddVideoTutorialAsync(
            It.Is<VideoTutorial>(v => v.CoachId == coachId && v.VideoUrl == "https://cdn.fitsocial.com/videos/incline-fly.mp4"),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockPlanRepo.Verify(r => r.AddAsync(It.IsAny<TrainingPlan>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateTrainingPlanAsync_WhenOrderBelongsToAnotherCoach_ShouldThrowForbiddenException()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var anotherCoachId = Guid.NewGuid();
        var orderDetailsId = Guid.NewGuid();

        var orderDetail = new OrderDetail
        {
            OrderDetailsId = orderDetailsId,
            Order = new Order
            {
                OrderId = Guid.NewGuid(),
                CoachId = anotherCoachId // Order owned by different coach
            },
            Package = new TrainingPackage
            {
                PackageId = Guid.NewGuid(),
                CoachId = anotherCoachId
            }
        };

        _mockPlanRepo
            .Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(orderDetailsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderDetail);

        var dto = new CreateTrainingPlanDto
        {
            OrderDetailsId = orderDetailsId,
            Title = "Kế hoạch trái phép"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.CreateTrainingPlanAsync(coachId, dto));

        Assert.Contains("permission", ex.Message, StringComparison.OrdinalIgnoreCase);
        _mockPlanRepo.Verify(r => r.AddAsync(It.IsAny<TrainingPlan>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateTrainingPlanAsync_WhenOrderDetailNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var orderDetailsId = Guid.NewGuid();

        _mockPlanRepo
            .Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(orderDetailsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderDetail?)null);

        var dto = new CreateTrainingPlanDto
        {
            OrderDetailsId = orderDetailsId,
            Title = "Kế hoạch không tồn tại order"
        };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateTrainingPlanAsync(coachId, dto));
    }

    [Fact]
    public async Task UpdateTrainingPlanAsync_ValidUpdate_ShouldUpdateFieldsAndExercises()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var existingExerciseId = Guid.NewGuid();

        var existingPlan = new TrainingPlan
        {
            TrainingPlanId = planId,
            CoachId = coachId,
            Title = "Tiêu đề cũ",
            Description = "Mô tả cũ",
            Status = "DRAFT",
            TrainingPlanExercises = new List<TrainingPlanExercise>
            {
                new()
                {
                    TrainingPlanExerciseId = existingExerciseId,
                    TrainingPlanId = planId,
                    ExerciseName = "Bài tập cũ",
                    WeekNumber = 1,
                    DayNumber = 1,
                    Sets = 3,
                    Reps = 8
                }
            }
        };

        _mockPlanRepo
            .Setup(r => r.GetByIdWithExercisesAsync(planId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPlan);

        _mockUow
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var updateDto = new UpdateTrainingPlanDto
        {
            Title = "Tiêu đề mới đã tối ưu",
            Description = "Mô tả cập nhật chi tiết",
            Status = "PUBLISHED",
            Exercises = new List<TrainingPlanExerciseDto>
            {
                new()
                {
                    TrainingPlanExerciseId = existingExerciseId,
                    ExerciseName = "Bài tập cũ đã đổi tên",
                    WeekNumber = 1,
                    DayNumber = 1,
                    Sets = 4,
                    Reps = 12
                },
                new()
                {
                    ExerciseName = "Bài tập mới thêm vào",
                    WeekNumber = 1,
                    DayNumber = 2,
                    Sets = 3,
                    Reps = 10,
                    NewVideoTutorial = new CreateVideoTutorialDto
                    {
                        Title = "Video bài mới",
                        VideoUrl = "https://cdn.fitsocial.com/videos/squat.mp4"
                    }
                }
            }
        };

        // Act
        var result = await _service.UpdateTrainingPlanAsync(coachId, planId, updateDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Tiêu đề mới đã tối ưu", existingPlan.Title);
        Assert.Equal("Mô tả cập nhật chi tiết", existingPlan.Description);
        Assert.Equal("PUBLISHED", existingPlan.Status);
        Assert.Equal(2, existingPlan.TrainingPlanExercises.Count);

        var updatedEx = existingPlan.TrainingPlanExercises.First(e => e.TrainingPlanExerciseId == existingExerciseId);
        Assert.Equal("Bài tập cũ đã đổi tên", updatedEx.ExerciseName);
        Assert.Equal((short)4, updatedEx.Sets);
        Assert.Equal((short)12, updatedEx.Reps);

        _mockPlanRepo.Verify(r => r.AddVideoTutorialAsync(
            It.Is<VideoTutorial>(v => v.VideoUrl == "https://cdn.fitsocial.com/videos/squat.mp4"),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateTrainingPlanAsync_WhenPlanBelongsToAnotherCoach_ShouldThrowForbiddenException()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var anotherCoachId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        var plan = new TrainingPlan
        {
            TrainingPlanId = planId,
            CoachId = anotherCoachId, // Not current coach
            Title = "Plan của Coach khác"
        };

        _mockPlanRepo
            .Setup(r => r.GetByIdWithExercisesAsync(planId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var dto = new UpdateTrainingPlanDto
        {
            Title = "Cố tình sửa"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.UpdateTrainingPlanAsync(coachId, planId, dto));

        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateTrainingPlanAsync_WhenPlanNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        _mockPlanRepo
            .Setup(r => r.GetByIdWithExercisesAsync(planId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainingPlan?)null);

        var dto = new UpdateTrainingPlanDto { Title = "Plan test" };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateTrainingPlanAsync(coachId, planId, dto));
    }

    [Fact]
    public async Task GetTrainingPlanByOrderDetailsIdAsync_ValidOwner_ShouldReturnPlan()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var orderDetailsId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        var orderDetail = new OrderDetail
        {
            OrderDetailsId = orderDetailsId,
            Order = new Order { CoachId = coachId }
        };

        var plan = new TrainingPlan
        {
            TrainingPlanId = planId,
            CoachId = coachId,
            OrderDetailsId = orderDetailsId,
            Title = "Lịch tập tháng 10",
            TrainingPlanExercises = new List<TrainingPlanExercise>
            {
                new()
                {
                    ExerciseName = "Deadlift",
                    WeekNumber = 1,
                    DayNumber = 1,
                    SortOrder = 1,
                    VideoTutorial = new VideoTutorial
                    {
                        Title = "Deadlift tutorial",
                        VideoUrl = "https://video.mp4"
                    }
                }
            }
        };

        _mockPlanRepo
            .Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(orderDetailsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderDetail);

        _mockPlanRepo
            .Setup(r => r.GetByOrderDetailsIdWithExercisesAsync(orderDetailsId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.GetTrainingPlanByOrderDetailsIdAsync(coachId, orderDetailsId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(planId, result!.TrainingPlanId);
        Assert.Single(result.Exercises);
        Assert.Equal("Deadlift", result.Exercises[0].ExerciseName);
        Assert.NotNull(result.Exercises[0].VideoTutorial);
        Assert.Equal("https://video.mp4", result.Exercises[0].VideoTutorial!.VideoUrl);
    }

    [Fact]
    public async Task GetTrainingPlanByOrderDetailsIdAsync_WhenNotOwner_ShouldThrowForbiddenException()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var anotherCoachId = Guid.NewGuid();
        var orderDetailsId = Guid.NewGuid();

        var orderDetail = new OrderDetail
        {
            OrderDetailsId = orderDetailsId,
            Order = new Order { CoachId = anotherCoachId }
        };

        _mockPlanRepo
            .Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(orderDetailsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderDetail);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.GetTrainingPlanByOrderDetailsIdAsync(coachId, orderDetailsId));
    }
}
