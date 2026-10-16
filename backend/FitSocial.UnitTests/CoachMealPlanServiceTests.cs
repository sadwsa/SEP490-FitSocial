using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.MealPlan;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class CoachMealPlanServiceTests
{
    private readonly Mock<IMealPlanRepository> _mockPlanRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly MealPlanService _service;

    public CoachMealPlanServiceTests()
    {
        _mockPlanRepo = new Mock<IMealPlanRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _service = new MealPlanService(_mockPlanRepo.Object, _mockUow.Object);
    }

    [Fact]
    public async Task CreateMealPlanAsync_ValidPlan_ShouldPersistPlanAndReturnDetails()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var orderDetailsId = Guid.NewGuid();
        var purchaseDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        var orderDetail = new OrderDetail
        {
            OrderDetailsId = orderDetailsId,
            PackageDurationDays = 30,
            PackageType = "BOTH",
            Order = new Order { CoachId = coachId, CreatedAt = purchaseDate },
            Package = new TrainingPackage { CoachId = coachId, DurationDays = 30, PackageType = "BOTH" }
        };

        _mockPlanRepo
            .Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(orderDetailsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderDetail);

        MealPlan? savedPlan = null;
        _mockPlanRepo
            .Setup(r => r.AddAsync(It.IsAny<MealPlan>(), It.IsAny<CancellationToken>()))
            .Callback<MealPlan, CancellationToken>((p, _) => savedPlan = p)
            .Returns(Task.CompletedTask);

        _mockPlanRepo
            .Setup(r => r.GetByIdWithItemsAsync(It.IsAny<Guid>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, bool _, CancellationToken __) => savedPlan);

        var dto = new CreateMealPlanDto
        {
            OrderDetailsId = orderDetailsId,
            Title = "Thực đơn mẫu 30 ngày",
            Description = "Dinh dưỡng chuẩn thể hình",
            StartDate = purchaseDate,
            EndDate = purchaseDate.AddDays(27),
            Status = "ACTIVE",
            Items = new List<CreateMealPlanItemDto>
            {
                new() { WeekNumber = 1, DayNumber = 1, MealType = "Breakfast", FoodDescription = "Yến mạch + 3 trứng", Calories = 450 },
                new() { WeekNumber = 1, DayNumber = 1, MealType = "Lunch", FoodDescription = "Ức gà + cơm gạo lứt", Calories = 600 }
            }
        };

        // Act
        var result = await _service.CreateMealPlanAsync(coachId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Thực đơn mẫu 30 ngày", result.Title);
        Assert.Equal(coachId, result.CoachId);
        Assert.Single(result.Weeks);
        Assert.Single(result.Weeks[0].Days);
        Assert.Equal(2, result.Weeks[0].Days[0].Items.Count);
        Assert.Equal(1050, result.Weeks[0].Days[0].TotalCalories);

        _mockPlanRepo.Verify(r => r.AddAsync(It.IsAny<MealPlan>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateMealPlanAsync_WhenPackageIsWorkoutOnly_ShouldThrowValidationException()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var orderDetailsId = Guid.NewGuid();
        var purchaseDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        var orderDetail = new OrderDetail
        {
            OrderDetailsId = orderDetailsId,
            PackageDurationDays = 30,
            PackageType = "WORKOUT",
            Order = new Order { CoachId = coachId, CreatedAt = purchaseDate },
            Package = new TrainingPackage { CoachId = coachId, DurationDays = 30, PackageType = "WORKOUT" }
        };

        _mockPlanRepo
            .Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(orderDetailsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderDetail);

        var dto = new CreateMealPlanDto
        {
            OrderDetailsId = orderDetailsId,
            Title = "Thực đơn",
            StartDate = purchaseDate,
            EndDate = purchaseDate.AddDays(27)
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateMealPlanAsync(coachId, dto));
        Assert.Contains("WORKOUT only", ex.Message);
    }

    [Fact]
    public async Task CreateMealPlanAsync_WhenOrderBelongsToAnotherCoach_ShouldThrowForbiddenException()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var anotherCoachId = Guid.NewGuid();
        var orderDetailsId = Guid.NewGuid();

        var orderDetail = new OrderDetail
        {
            OrderDetailsId = orderDetailsId,
            Order = new Order { CoachId = anotherCoachId },
            Package = new TrainingPackage { CoachId = anotherCoachId }
        };

        _mockPlanRepo
            .Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(orderDetailsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderDetail);

        var dto = new CreateMealPlanDto { OrderDetailsId = orderDetailsId };

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.CreateMealPlanAsync(coachId, dto));
    }

    [Fact]
    public async Task CreateMealPlanAsync_WhenOrderDetailNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var orderDetailsId = Guid.NewGuid();

        _mockPlanRepo
            .Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(orderDetailsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderDetail?)null);

        var dto = new CreateMealPlanDto { OrderDetailsId = orderDetailsId };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateMealPlanAsync(coachId, dto));
    }

    [Fact]
    public async Task CreateMealPlanAsync_WhenStartDateBeforePurchaseDate_ShouldThrowValidationException()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var orderDetailsId = Guid.NewGuid();
        var purchaseDate = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);

        var orderDetail = new OrderDetail
        {
            OrderDetailsId = orderDetailsId,
            PackageDurationDays = 30,
            PackageType = "BOTH",
            Order = new Order { CoachId = coachId, CreatedAt = purchaseDate },
            Package = new TrainingPackage { CoachId = coachId, DurationDays = 30 }
        };

        _mockPlanRepo
            .Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(orderDetailsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderDetail);

        var dto = new CreateMealPlanDto
        {
            OrderDetailsId = orderDetailsId,
            Title = "Thực đơn",
            StartDate = purchaseDate.AddDays(-2),
            EndDate = purchaseDate.AddDays(25)
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateMealPlanAsync(coachId, dto));
        Assert.Contains("cannot be earlier than package purchase date", ex.Message);
    }

    [Fact]
    public async Task CreateMealPlanAsync_WhenEndDateAfterPackageExpiration_ShouldThrowValidationException()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var orderDetailsId = Guid.NewGuid();
        var purchaseDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        var orderDetail = new OrderDetail
        {
            OrderDetailsId = orderDetailsId,
            PackageDurationDays = 30,
            PackageType = "BOTH",
            Order = new Order { CoachId = coachId, CreatedAt = purchaseDate },
            Package = new TrainingPackage { CoachId = coachId, DurationDays = 30 }
        };

        _mockPlanRepo
            .Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(orderDetailsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderDetail);

        var dto = new CreateMealPlanDto
        {
            OrderDetailsId = orderDetailsId,
            Title = "Thực đơn",
            StartDate = purchaseDate,
            EndDate = purchaseDate.AddDays(35) // 35 > 30 days
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateMealPlanAsync(coachId, dto));
        Assert.Contains("cannot exceed package expiration date", ex.Message);
    }

    [Fact]
    public async Task CreateMealPlanAsync_WhenPlanExceedsMaxWeeks_ShouldThrowValidationException()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var orderDetailsId = Guid.NewGuid();
        var purchaseDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        var orderDetail = new OrderDetail
        {
            OrderDetailsId = orderDetailsId,
            PackageDurationDays = 30,
            PackageType = "BOTH",
            Order = new Order { CoachId = coachId, CreatedAt = purchaseDate },
            Package = new TrainingPackage { CoachId = coachId, DurationDays = 30 }
        };

        _mockPlanRepo
            .Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(orderDetailsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderDetail);

        var dto = new CreateMealPlanDto
        {
            OrderDetailsId = orderDetailsId,
            Title = "Thực đơn 5 tuần",
            StartDate = purchaseDate,
            EndDate = purchaseDate.AddDays(29), // ~4 weeks
            Items = new List<CreateMealPlanItemDto>
            {
                new() { WeekNumber = 5, DayNumber = 1, FoodDescription = "Bữa ăn tuần 5", MealType = "Lunch" }
            }
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateMealPlanAsync(coachId, dto));
        Assert.Contains("cannot exceed 4 weeks", ex.Message);
    }

    [Fact]
    public async Task CreateMealPlanAsync_WhenUpcomingWeekIsEmpty_ShouldThrowValidationException()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var orderDetailsId = Guid.NewGuid();
        var purchaseDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        var orderDetail = new OrderDetail
        {
            OrderDetailsId = orderDetailsId,
            PackageDurationDays = 30,
            PackageType = "BOTH",
            Order = new Order { CoachId = coachId, CreatedAt = purchaseDate },
            Package = new TrainingPackage { CoachId = coachId, DurationDays = 30 }
        };

        _mockPlanRepo
            .Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(orderDetailsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderDetail);

        var dto = new CreateMealPlanDto
        {
            OrderDetailsId = orderDetailsId,
            Title = "Thực đơn bỏ trống tuần 1",
            StartDate = purchaseDate,
            EndDate = purchaseDate.AddDays(28),
            // Only week 2 scheduled, week 1 is empty!
            Items = new List<CreateMealPlanItemDto>
            {
                new() { WeekNumber = 2, DayNumber = 1, FoodDescription = "Bữa ăn tuần 2", MealType = "Lunch" }
            }
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateMealPlanAsync(coachId, dto));
        Assert.Contains("Week 1 (starting week) cannot be empty", ex.Message);
    }

    [Fact]
    public async Task UpdateMealPlanAsync_WhenModifyingPastOrTodayMeals_ShouldThrowValidationException()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var pastMealId = Guid.NewGuid();
        var planStart = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var today = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc); // Day 3

        var existingPlan = new MealPlan
        {
            MealPlanId = planId,
            CoachId = coachId,
            StartDate = planStart,
            EndDate = planStart.AddDays(28),
            MealPlanItems = new List<MealPlanItem>
            {
                // Day 1 (2026-10-01) <= Today (2026-10-03) => in the past!
                new()
                {
                    MealPlanItemId = pastMealId,
                    WeekNumber = 1,
                    DayNumber = 1,
                    MealType = "Breakfast",
                    FoodDescription = "Trứng luộc",
                    Calories = 200
                }
            }
        };

        _mockPlanRepo
            .Setup(r => r.GetByIdWithItemsAsync(planId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPlan);

        // Attempt to modify the description of past meal item
        var updateDto = new UpdateMealPlanDto
        {
            Items = new List<UpdateMealPlanItemDto>
            {
                new()
                {
                    MealPlanItemId = pastMealId,
                    WeekNumber = 1,
                    DayNumber = 1,
                    MealType = "Breakfast",
                    FoodDescription = "Bánh mì kẹp thịt (Modified!)",
                    Calories = 500
                },
                new()
                {
                    WeekNumber = 2,
                    DayNumber = 1,
                    MealType = "Lunch",
                    FoodDescription = "Cơm trưa tuần 2",
                    Calories = 600
                }
            }
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateMealPlanInternalAsync(coachId, planId, updateDto, asOfDate: today));
        Assert.Contains("Cannot modify meal item", ex.Message);
    }

    [Fact]
    public async Task UpdateMealPlanAsync_ValidUpdate_ShouldUpdateFieldsAndItems()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var existingItemId = Guid.NewGuid();
        var planStart = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc); // in future
        var today = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);

        var existingPlan = new MealPlan
        {
            MealPlanId = planId,
            CoachId = coachId,
            Title = "Thực đơn cũ",
            StartDate = planStart,
            EndDate = planStart.AddDays(28),
            MealPlanItems = new List<MealPlanItem>
            {
                new()
                {
                    MealPlanItemId = existingItemId,
                    WeekNumber = 1,
                    DayNumber = 1,
                    MealType = "Breakfast",
                    FoodDescription = "Yến mạch",
                    Calories = 300
                }
            }
        };

        _mockPlanRepo
            .Setup(r => r.GetByIdWithItemsAsync(planId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPlan);

        _mockPlanRepo
            .Setup(r => r.GetByIdWithItemsAsync(planId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPlan);

        var updateDto = new UpdateMealPlanDto
        {
            Title = "Thực đơn mới cập nhật",
            Items = new List<UpdateMealPlanItemDto>
            {
                new()
                {
                    MealPlanItemId = existingItemId,
                    WeekNumber = 1,
                    DayNumber = 1,
                    MealType = "Breakfast",
                    FoodDescription = "Yến mạch + Táo đỏ",
                    Calories = 350
                },
                new()
                {
                    WeekNumber = 1,
                    DayNumber = 2,
                    MealType = "Lunch",
                    FoodDescription = "Cá hồi áp chảo",
                    Calories = 550
                }
            }
        };

        // Act
        var result = await _service.UpdateMealPlanInternalAsync(coachId, planId, updateDto, asOfDate: today);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Thực đơn mới cập nhật", existingPlan.Title);
        Assert.Equal(2, existingPlan.MealPlanItems.Count);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
