using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Claims;
using FitSocial.API.Controllers;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
            Title = "Thá»±c Ä‘Æ¡n máº«u 30 ngÃ y",
            Description = "Dinh dÆ°á»¡ng chuáº©n thá»ƒ hÃ¬nh",
            StartDate = purchaseDate,
            EndDate = purchaseDate.AddDays(27),
            Status = "ACTIVE",
            Items = new List<CreateMealPlanItemDto>
            {
                new() { WeekNumber = 1, DayNumber = 1, MealType = "Breakfast", FoodDescription = "Yáº¿n máº¡ch + 3 trá»©ng", Calories = 450 },
                new() { WeekNumber = 1, DayNumber = 1, MealType = "Lunch", FoodDescription = "á»¨c gÃ  + cÆ¡m gáº¡o lá»©t", Calories = 600 }
            }
        };

        // Act
        var result = await _service.CreateMealPlanAsync(coachId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Thá»±c Ä‘Æ¡n máº«u 30 ngÃ y", result.Title);
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
            Title = "Thá»±c Ä‘Æ¡n",
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
            Title = "Thá»±c Ä‘Æ¡n",
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
            Title = "Thá»±c Ä‘Æ¡n",
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
            Title = "Thá»±c Ä‘Æ¡n 5 tuáº§n",
            StartDate = purchaseDate,
            EndDate = purchaseDate.AddDays(29), // ~4 weeks
            Items = new List<CreateMealPlanItemDto>
            {
                new() { WeekNumber = 5, DayNumber = 1, FoodDescription = "Bá»¯a Äƒn tuáº§n 5", MealType = "Lunch" }
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
            Title = "Thá»±c Ä‘Æ¡n bá» trá»‘ng tuáº§n 1",
            StartDate = purchaseDate,
            EndDate = purchaseDate.AddDays(28),
            // Only week 2 scheduled, week 1 is empty!
            Items = new List<CreateMealPlanItemDto>
            {
                new() { WeekNumber = 2, DayNumber = 1, FoodDescription = "Bá»¯a Äƒn tuáº§n 2", MealType = "Lunch" }
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
                    FoodDescription = "Trá»©ng luá»™c",
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
                    FoodDescription = "BÃ¡nh mÃ¬ káº¹p thá»‹t (Modified!)",
                    Calories = 500
                },
                new()
                {
                    WeekNumber = 2,
                    DayNumber = 1,
                    MealType = "Lunch",
                    FoodDescription = "CÆ¡m trÆ°a tuáº§n 2",
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
            Title = "Thá»±c Ä‘Æ¡n cÅ©",
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
                    FoodDescription = "Yáº¿n máº¡ch",
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
            Title = "Thá»±c Ä‘Æ¡n má»›i cáº­p nháº­t",
            Items = new List<UpdateMealPlanItemDto>
            {
                new()
                {
                    MealPlanItemId = existingItemId,
                    WeekNumber = 1,
                    DayNumber = 1,
                    MealType = "Breakfast",
                    FoodDescription = "Yáº¿n máº¡ch + TÃ¡o Ä‘á»",
                    Calories = 350
                },
                new()
                {
                    WeekNumber = 1,
                    DayNumber = 2,
                    MealType = "Lunch",
                    FoodDescription = "CÃ¡ há»“i Ã¡p cháº£o",
                    Calories = 550
                }
            }
        };

        // Act
        var result = await _service.UpdateMealPlanInternalAsync(coachId, planId, updateDto, asOfDate: today);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Thá»±c Ä‘Æ¡n má»›i cáº­p nháº­t", existingPlan.Title);
        Assert.Equal(2, existingPlan.MealPlanItems.Count);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }


    [Fact]
    public async Task UpdateMealPlanAsync_WhenCoachIdIsEmpty_ShouldThrowForbiddenException()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.UpdateMealPlanAsync(Guid.Empty, Guid.NewGuid(), new UpdateMealPlanDto()));
        Assert.Equal("Invalid coach identity.", ex.Message);
    }

    [Fact]
    public async Task UpdateMealPlanAsync_WhenDtoIsNull_ShouldThrowValidationException()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateMealPlanAsync(Guid.NewGuid(), Guid.NewGuid(), null!));
        Assert.Equal("Request body cannot be null.", ex.Message);
    }

    [Fact]
    public async Task UpdateMealPlanAsync_WhenPlanNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        _mockPlanRepo
            .Setup(r => r.GetByIdWithItemsAsync(planId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MealPlan?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateMealPlanAsync(coachId, planId, new UpdateMealPlanDto()));
        Assert.Equal("Meal plan not found.", ex.Message);
    }

    [Fact]
    public async Task UpdateMealPlanAsync_WhenPlanBelongsToAnotherCoach_ShouldThrowForbiddenException()
    {
        // Arrange
        var coachAId = Guid.NewGuid();
        var coachBId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        var planOfCoachA = new MealPlan
        {
            MealPlanId = planId,
            CoachId = coachAId
        };

        _mockPlanRepo
            .Setup(r => r.GetByIdWithItemsAsync(planId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(planOfCoachA);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.UpdateMealPlanAsync(coachBId, planId, new UpdateMealPlanDto()));
        Assert.Contains("do not have permission to update this meal plan", ex.Message);
    }

    [Fact]
    public async Task GetCoachMealPlanByOrderDetailsAsync_WhenNotOrderOwner_ShouldThrowForbiddenException()
    {
        // Arrange
        var coachAId = Guid.NewGuid();
        var coachBId = Guid.NewGuid();
        var orderDetailsId = Guid.NewGuid();

        var plan = new MealPlan
        {
            MealPlanId = Guid.NewGuid(),
            CoachId = coachAId,
            OrderDetailsId = orderDetailsId,
            OrderDetails = new OrderDetail
            {
                OrderDetailsId = orderDetailsId,
                Order = new Order { CoachId = coachAId }
            }
        };

        _mockPlanRepo
            .Setup(r => r.GetMealPlanByOrderDetailsAsync(orderDetailsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.GetCoachMealPlanByOrderDetailsAsync(coachBId, orderDetailsId));
        Assert.Contains("do not have permission", ex.Message);
    }

    #region Controller Tests (CoachMealPlansController)

    [Fact]
    public void CoachMealPlansController_HasAuthorizeAttribute_WithCoachRoleOnly()
    {
        // Arrange & Act
        var attr = typeof(CoachMealPlansController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        // Assert
        Assert.NotNull(attr);
        Assert.Equal("COACH", attr.Roles);
    }

    [Fact]
    public async Task CreateMealPlan_Controller_WhenCoachAuthenticated_ReturnsOk()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var mockService = new Mock<IMealPlanService>();
        var mockLogger = new Mock<ILogger<CoachMealPlansController>>();
        var controller = new CoachMealPlansController(mockService.Object, mockLogger.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, coachId.ToString()),
            new Claim(ClaimTypes.Role, "COACH")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        var dto = new CreateMealPlanDto { Title = "Nutrition plan" };
        var expected = new TraineeMealPlanDto { MealPlanId = Guid.NewGuid(), Title = "Nutrition plan" };

        mockService.Setup(s => s.CreateMealPlanAsync(coachId, dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await controller.CreateMealPlan(dto);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponseDto<TraineeMealPlanDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal(expected.MealPlanId, response.Data?.MealPlanId);
    }

    [Fact]
    public async Task CreateMealPlan_Controller_WhenTokenMissingOrInvalid_ReturnsUnauthorized()
    {
        // Arrange
        var mockService = new Mock<IMealPlanService>();
        var mockLogger = new Mock<ILogger<CoachMealPlansController>>();
        var controller = new CoachMealPlansController(mockService.Object, mockLogger.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        // Act
        var result = await controller.CreateMealPlan(new CreateMealPlanDto());

        // Assert
        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var response = Assert.IsType<ApiResponseDto<TraineeMealPlanDto>>(unauthorized.Value);
        Assert.False(response.Success);
        Assert.Contains("Invalid coach identity", response.Message);
    }

    [Fact]
    public async Task UpdateMealPlan_Controller_WhenCoachAuthenticated_ReturnsOk()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var mockService = new Mock<IMealPlanService>();
        var mockLogger = new Mock<ILogger<CoachMealPlansController>>();
        var controller = new CoachMealPlansController(mockService.Object, mockLogger.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, coachId.ToString()),
            new Claim(ClaimTypes.Role, "COACH")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        var dto = new UpdateMealPlanDto { Title = "Updated plan" };
        var expected = new TraineeMealPlanDto { MealPlanId = planId, Title = "Updated plan" };

        mockService.Setup(s => s.UpdateMealPlanAsync(coachId, planId, dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await controller.UpdateMealPlan(planId, dto);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponseDto<TraineeMealPlanDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("Updated plan", response.Data?.Title);
    }

    [Fact]
    public async Task GetMealPlanByOrderDetail_Controller_WhenCoachAuthenticated_ReturnsOk()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var orderDetailsId = Guid.NewGuid();
        var mockService = new Mock<IMealPlanService>();
        var mockLogger = new Mock<ILogger<CoachMealPlansController>>();
        var controller = new CoachMealPlansController(mockService.Object, mockLogger.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, coachId.ToString()),
            new Claim(ClaimTypes.Role, "COACH")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        var expected = new TraineeMealPlanDto { OrderDetailsId = orderDetailsId, Title = "Current Meal Plan" };
        mockService.Setup(s => s.GetCoachMealPlanByOrderDetailsAsync(coachId, orderDetailsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await controller.GetMealPlanByOrderDetail(orderDetailsId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponseDto<TraineeMealPlanDto?>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal(orderDetailsId, response.Data?.OrderDetailsId);
    }
    #endregion
}
