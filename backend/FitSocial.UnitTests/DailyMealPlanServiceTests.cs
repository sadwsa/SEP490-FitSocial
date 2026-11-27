using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.MealPlan;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

/// <summary>
/// Unit test suite for Daily Meal Plan feature (Trainee daily meal schedule, proof submission, missed reason):
/// 1. MealPlanService.GetTraineeMealPlanAsync (14 tests: 5 N, 4 B, 5 A)
/// 2. MealPlanService.RequestMealCompletionAsync (17 tests: 4 N, 5 B, 8 A)
/// 3. MealPlanService.SaveMealMissedReasonAsync (11 tests: 2 N, 3 B, 6 A)
/// </summary>
public class DailyMealPlanServiceTests
{
    private readonly Mock<IMealPlanRepository> _mockPlanRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<INotificationRepository> _mockNotificationRepo;
    private readonly MealPlanService _service;

    public DailyMealPlanServiceTests()
    {
        _mockPlanRepo = new Mock<IMealPlanRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockNotificationRepo = new Mock<INotificationRepository>();

        _service = new MealPlanService(
            _mockPlanRepo.Object,
            _mockUow.Object,
            _mockNotificationRepo.Object);
    }

    #region Helper Factories

    private static (MealPlan Plan, MealPlanItem Item) CreateMealHierarchy(
        Guid traineeId,
        Guid? coachId = null,
        DateTime? planStartDate = null,
        DateTime? planEndDate = null,
        short weekNumber = 1,
        short dayNumber = 1,
        string orderStatus = "PAID",
        string planStatus = "ACTIVE",
        Guid? orderDetailsId = null,
        Guid? mealItemId = null)
    {
        var cId = coachId ?? Guid.NewGuid();
        var odId = orderDetailsId ?? Guid.NewGuid();
        var itemId = mealItemId ?? Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var buyer = new User
        {
            UserId = traineeId,
            FullName = "Trainee Minh",
            Email = "minh@fitsocial.com"
        };

        var coachUser = new User
        {
            UserId = cId,
            FullName = "Coach Lan",
            Email = "lan@fitsocial.com"
        };

        var order = new Order
        {
            OrderId = orderId,
            BuyerId = traineeId,
            Buyer = buyer,
            CoachId = cId,
            OrderStatus = orderStatus
        };

        var pkg = new TrainingPackage
        {
            PackageId = Guid.NewGuid(),
            CoachId = cId,
            Title = "Lean Bulk Nutrition 30D",
            PackageType = "BOTH",
            Media = new List<TrainingPackageMedium>
            {
                new() { SortOrder = 0, MediaUrl = "https://cdn.fitsocial.test/thumb.jpg" }
            }
        };

        var orderDetail = new OrderDetail
        {
            OrderDetailsId = odId,
            OrderId = orderId,
            Order = order,
            PackageId = pkg.PackageId,
            Package = pkg,
            PackageTitle = "Lean Bulk Nutrition 30D",
            PackageType = "BOTH",
            CoachName = "Coach Lan"
        };

        var coachProfile = new CoachProfile
        {
            CoachId = cId,
            Coach = coachUser,
            Status = "ACTIVE"
        };

        var plan = new MealPlan
        {
            MealPlanId = Guid.NewGuid(),
            CoachId = cId,
            Coach = coachProfile,
            OrderDetailsId = odId,
            OrderDetails = orderDetail,
            Title = "Daily High-Protein Meal Plan",
            Description = "Balanced macros for muscle gain",
            StartDate = planStartDate,
            EndDate = planEndDate,
            Status = planStatus,
            MealPlanItems = new List<MealPlanItem>()
        };

        var item = new MealPlanItem
        {
            MealPlanItemId = itemId,
            MealPlanId = plan.MealPlanId,
            MealPlan = plan,
            WeekNumber = weekNumber,
            DayNumber = dayNumber,
            MealType = "Breakfast",
            FoodDescription = "Oatmeal 80g + 3 boiled eggs",
            Calories = 480,
            SortOrder = 1,
            MealCompletionLogs = new List<MealCompletionLog>()
        };

        plan.MealPlanItems.Add(item);
        return (plan, item);
    }

    #endregion

    #region 1. GetTraineeMealPlanAsync (14 Tests: 5 N, 4 B, 5 A)

    [Fact]
    public async Task GetTraineeMealPlanAsync_ActivePlanWithCompletedAndPendingMeals_ReturnsGroupedWeeksCaloriesAndAdherence()
    {
        // UTCID01 - Normal: Returns grouped weeks/days, daily calories, completion count, and adherence rate
        var traineeId = Guid.NewGuid();
        var today = MealPlanService.GetBusinessToday();
        short todayDay = today.DayOfWeek == DayOfWeek.Sunday ? (short)7 : (short)today.DayOfWeek;
        var (plan, breakfast) = CreateMealHierarchy(traineeId, planStartDate: today, planEndDate: today.AddDays(28), weekNumber: 1, dayNumber: todayDay);

        breakfast.MealCompletionLogs.Add(new MealCompletionLog
        {
            MealCompletionLogId = Guid.NewGuid(),
            TraineeId = traineeId,
            MealPlanItemId = breakfast.MealPlanItemId,
            Status = "COMPLETED",
            CreatedAt = DateTime.UtcNow
        });

        var lunch = new MealPlanItem
        {
            MealPlanItemId = Guid.NewGuid(),
            MealPlanId = plan.MealPlanId,
            MealPlan = plan,
            WeekNumber = 1,
            DayNumber = todayDay,
            MealType = "Lunch",
            FoodDescription = "Chicken breast 200g + Brown rice",
            Calories = 620,
            SortOrder = 2,
            MealCompletionLogs = new List<MealCompletionLog>()
        };
        plan.MealPlanItems.Add(lunch);

        _mockPlanRepo.Setup(r => r.GetMealPlanByOrderDetailsAsync(plan.OrderDetailsId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var result = await _service.GetTraineeMealPlanAsync(traineeId, plan.OrderDetailsId.Value);

        Assert.NotNull(result);
        Assert.Equal(plan.MealPlanId, result!.MealPlanId);
        Assert.Equal("Daily High-Protein Meal Plan", result.Title);
        Assert.Equal("Coach Lan", result.CoachName);
        Assert.Equal("https://cdn.fitsocial.test/thumb.jpg", result.PackageThumbnailUrl);
        Assert.Equal(2, result.TotalMealsCount);
        Assert.Equal(1, result.CompletedMealsCount);
        Assert.Equal(0, result.MissedMealsCount);
        Assert.Equal(100.0, result.AdherenceRate);
        Assert.Equal(50.0, result.ProgressPercentage);
        Assert.Single(result.Weeks);
        Assert.Single(result.Weeks[0].Days);
        Assert.True(result.Weeks[0].Days[0].IsToday);
        Assert.Equal(1100, result.Weeks[0].Days[0].TotalCalories);
    }

    [Fact]
    public async Task GetTraineeMealPlanAsync_ActivePlanWithPastUnloggedMeal_BackfillsIncompleteLog()
    {
        // UTCID02 - Normal: Unlogged meal scheduled in the past is auto-backfilled as INCOMPLETE
        var traineeId = Guid.NewGuid();
        var pastStart = MealPlanService.GetBusinessToday().AddDays(-14);
        var (plan, item) = CreateMealHierarchy(traineeId, planStartDate: pastStart, planEndDate: pastStart.AddDays(28), weekNumber: 1, dayNumber: 1);

        _mockPlanRepo.Setup(r => r.GetMealPlanByOrderDetailsAsync(plan.OrderDetailsId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var result = await _service.GetTraineeMealPlanAsync(traineeId, plan.OrderDetailsId.Value);

        Assert.NotNull(result);
        Assert.Equal(1, result!.MissedMealsCount);
        Assert.Equal(0, result.CompletedMealsCount);
        Assert.Equal(0.0, result.AdherenceRate);
        _mockPlanRepo.Verify(r => r.AddMealLogAsync(It.Is<MealCompletionLog>(l =>
            l.TraineeId == traineeId &&
            l.MealPlanItemId == item.MealPlanItemId &&
            l.Status == "INCOMPLETE"), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTraineeMealPlanAsync_NoPlanYetWithValidOrderDetail_ReturnsPendingCoachPlaceholder()
    {
        // UTCID03 - Normal: When coach hasn't created meal plan yet, returns PENDING_COACH placeholder DTO
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var odId = Guid.NewGuid();
        var orderDetail = new OrderDetail
        {
            OrderDetailsId = odId,
            PackageTitle = "Fat Loss 30 Days",
            PackageType = "MEAL",
            CoachName = "Coach An",
            Order = new Order { BuyerId = traineeId, CoachId = coachId, OrderStatus = PaymentConstants.OrderStatusPaid }
        };

        _mockPlanRepo.Setup(r => r.GetMealPlanByOrderDetailsAsync(odId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MealPlan?)null);
        _mockPlanRepo.Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(odId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderDetail);

        var result = await _service.GetTraineeMealPlanAsync(traineeId, odId);

        Assert.NotNull(result);
        Assert.Equal(Guid.Empty, result!.MealPlanId);
        Assert.Equal(odId, result.OrderDetailsId);
        Assert.Equal("PENDING_COACH", result.Status);
        Assert.Equal("Fat Loss 30 Days", result.PackageTitle);
        Assert.Equal("MEAL", result.PackageType);
        Assert.Equal("Coach An", result.CoachName);
    }

    [Fact]
    public async Task GetTraineeMealPlanAsync_DraftPlan_SkipsBackfill()
    {
        // UTCID04 - Normal: Plan in DRAFT status does not trigger backfill even if StartDate is in the past
        var traineeId = Guid.NewGuid();
        var pastStart = MealPlanService.GetBusinessToday().AddDays(-14);
        var (plan, _) = CreateMealHierarchy(traineeId, planStartDate: pastStart, planEndDate: pastStart.AddDays(28), planStatus: "DRAFT");

        _mockPlanRepo.Setup(r => r.GetMealPlanByOrderDetailsAsync(plan.OrderDetailsId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var result = await _service.GetTraineeMealPlanAsync(traineeId, plan.OrderDetailsId.Value);

        Assert.NotNull(result);
        Assert.Equal("DRAFT", result!.Status);
        Assert.Equal(0, result.MissedMealsCount);
        _mockPlanRepo.Verify(r => r.AddMealLogAsync(It.IsAny<MealCompletionLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("PAID")]
    [InlineData("ACTIVE")]
    [InlineData("COMPLETED")]
    [InlineData("REFUND_PENDING")]
    public async Task GetTraineeMealPlanAsync_AllowedOrderStatuses_Succeeds(string orderStatus)
    {
        // UTCID05 - Normal: All 4 allowed trainee order statuses grant access to the daily meal plan
        var traineeId = Guid.NewGuid();
        var (plan, _) = CreateMealHierarchy(traineeId, orderStatus: orderStatus);

        _mockPlanRepo.Setup(r => r.GetMealPlanByOrderDetailsAsync(plan.OrderDetailsId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var result = await _service.GetTraineeMealPlanAsync(traineeId, plan.OrderDetailsId.Value);

        Assert.NotNull(result);
        Assert.Equal(plan.MealPlanId, result!.MealPlanId);
    }

    [Fact]
    public async Task GetTraineeMealPlanAsync_PlanWithZeroMealItems_ReturnsZeroCountsAndZeroAdherence()
    {
        // UTCID06 - Boundary: Plan with 0 meal items returns TotalMealsCount=0 and AdherenceRate=0
        var traineeId = Guid.NewGuid();
        var (plan, _) = CreateMealHierarchy(traineeId);
        plan.MealPlanItems.Clear();

        _mockPlanRepo.Setup(r => r.GetMealPlanByOrderDetailsAsync(plan.OrderDetailsId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var result = await _service.GetTraineeMealPlanAsync(traineeId, plan.OrderDetailsId.Value);

        Assert.NotNull(result);
        Assert.Equal(0, result!.TotalMealsCount);
        Assert.Equal(0, result.CompletedMealsCount);
        Assert.Equal(0.0, result.AdherenceRate);
        Assert.Empty(result.Weeks);
    }

    [Fact]
    public async Task GetTraineeMealPlanAsync_FlexiblePlanWithoutStartDate_SkipsBackfillAndSetsIsTodayTrue()
    {
        // UTCID07 - Boundary: Flexible plan (StartDate == null) skips backfill and marks every day IsToday=true
        var traineeId = Guid.NewGuid();
        var (plan, _) = CreateMealHierarchy(traineeId, planStartDate: null, planEndDate: null);

        _mockPlanRepo.Setup(r => r.GetMealPlanByOrderDetailsAsync(plan.OrderDetailsId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var result = await _service.GetTraineeMealPlanAsync(traineeId, plan.OrderDetailsId.Value);

        Assert.NotNull(result);
        Assert.Single(result!.Weeks);
        Assert.True(result.Weeks[0].Days[0].IsToday);
        Assert.Null(result.Weeks[0].Days[0].ScheduledDate);
        _mockPlanRepo.Verify(r => r.AddMealLogAsync(It.IsAny<MealCompletionLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetTraineeMealPlanAsync_MealsScheduledPastEndDate_DropsOutOfRangeDays()
    {
        // UTCID08 - Boundary: Meals scheduled after plan EndDate are dropped for trainee view
        var traineeId = Guid.NewGuid();
        var today = MealPlanService.GetBusinessToday();
        var (plan, _) = CreateMealHierarchy(traineeId, planStartDate: today, planEndDate: today.AddDays(6), weekNumber: 3, dayNumber: 1);

        _mockPlanRepo.Setup(r => r.GetMealPlanByOrderDetailsAsync(plan.OrderDetailsId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var result = await _service.GetTraineeMealPlanAsync(traineeId, plan.OrderDetailsId.Value);

        Assert.NotNull(result);
        Assert.Empty(result!.Weeks);
        Assert.Equal(0, result.TotalMealsCount);
    }

    [Fact]
    public async Task GetTraineeMealPlanAsync_OrderDetailNotFoundAndPlanNull_ReturnsNull()
    {
        // UTCID09 - Boundary: Neither meal plan nor order detail exists -> returns null
        var traineeId = Guid.NewGuid();
        var odId = Guid.NewGuid();

        _mockPlanRepo.Setup(r => r.GetMealPlanByOrderDetailsAsync(odId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MealPlan?)null);
        _mockPlanRepo.Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(odId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderDetail?)null);

        var result = await _service.GetTraineeMealPlanAsync(traineeId, odId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetTraineeMealPlanAsync_EmptyTraineeId_ThrowsForbiddenException()
    {
        // UTCID10 - Abnormal: Guid.Empty traineeId -> ForbiddenException
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.GetTraineeMealPlanAsync(Guid.Empty, Guid.NewGuid()));

        Assert.Equal("Invalid trainee identity.", ex.Message);
        _mockPlanRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetTraineeMealPlanAsync_UnauthorizedTraineeOnExistingPlan_ThrowsForbiddenException()
    {
        // UTCID11 - Abnormal: Another trainee tries to view existing meal plan -> ForbiddenException
        var buyerId = Guid.NewGuid();
        var intruderId = Guid.NewGuid();
        var (plan, _) = CreateMealHierarchy(buyerId);

        _mockPlanRepo.Setup(r => r.GetMealPlanByOrderDetailsAsync(plan.OrderDetailsId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.GetTraineeMealPlanAsync(intruderId, plan.OrderDetailsId.Value));

        Assert.Equal("You do not have permission to view this meal plan.", ex.Message);
    }

    [Fact]
    public async Task GetTraineeMealPlanAsync_UnauthorizedTraineeOnPendingOrderDetail_ThrowsForbiddenException()
    {
        // UTCID12 - Abnormal: Another trainee queries order detail without meal plan -> ForbiddenException
        var buyerId = Guid.NewGuid();
        var intruderId = Guid.NewGuid();
        var odId = Guid.NewGuid();

        _mockPlanRepo.Setup(r => r.GetMealPlanByOrderDetailsAsync(odId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MealPlan?)null);
        _mockPlanRepo.Setup(r => r.GetOrderDetailWithOrderAndPackageAsync(odId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderDetail
            {
                OrderDetailsId = odId,
                Order = new Order { BuyerId = buyerId, OrderStatus = PaymentConstants.OrderStatusPaid }
            });

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.GetTraineeMealPlanAsync(intruderId, odId));

        Assert.Equal("You do not have permission to view this meal plan.", ex.Message);
    }

    [Fact]
    public async Task GetTraineeMealPlanAsync_RefundedOrPendingOrder_ThrowsForbiddenException()
    {
        // UTCID13 - Abnormal: REFUNDED or PENDING order blocks meal plan access
        var traineeId = Guid.NewGuid();
        var (refundedPlan, _) = CreateMealHierarchy(traineeId, orderStatus: PaymentConstants.OrderStatusRefunded);
        var (pendingPlan, _) = CreateMealHierarchy(traineeId, orderStatus: PaymentConstants.OrderStatusPending);

        _mockPlanRepo.Setup(r => r.GetMealPlanByOrderDetailsAsync(refundedPlan.OrderDetailsId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refundedPlan);
        _mockPlanRepo.Setup(r => r.GetMealPlanByOrderDetailsAsync(pendingPlan.OrderDetailsId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingPlan);

        var exRefunded = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.GetTraineeMealPlanAsync(traineeId, refundedPlan.OrderDetailsId.Value));
        var exPending = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.GetTraineeMealPlanAsync(traineeId, pendingPlan.OrderDetailsId.Value));

        Assert.Contains("refunded", exRefunded.Message);
        Assert.Contains("PENDING", exPending.Message);
    }

    [Fact]
    public async Task GetTraineeMealPlanAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // UTCID14 - Abnormal: Cancelled CancellationToken propagates OperationCanceledException
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var odId = Guid.NewGuid();

        _mockPlanRepo.Setup(r => r.GetMealPlanByOrderDetailsAsync(odId, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.GetTraineeMealPlanAsync(Guid.NewGuid(), odId, cts.Token));
    }

    #endregion

    #region 2. RequestMealCompletionAsync (17 Tests: 4 N, 5 B, 8 A)

    [Fact]
    public async Task RequestMealCompletionAsync_ValidSubmission_CreatesPendingReviewLogAndNotifiesCoach()
    {
        // UTCID01 - Normal: Creates PENDING_REVIEW MealCompletionLog and sends MEAL_COMPLETION_REQUESTED notification to coach
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var (plan, item) = CreateMealHierarchy(traineeId, coachId);

        _mockPlanRepo.Setup(r => r.GetMealItemByIdAsync(item.MealPlanItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        _mockPlanRepo.Setup(r => r.GetMealLogAsync(item.MealPlanItemId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MealCompletionLog?)null);
        _mockPlanRepo.Setup(r => r.GetMealPlanByOrderDetailsAsync(plan.OrderDetailsId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var dto = new RequestMealCompletionDto
        {
            MealPlanItemId = item.MealPlanItemId,
            MediaUrl = "https://cdn.fitsocial.test/meal_breakfast.jpg",
            Notes = "Ate full portion with black coffee."
        };

        var result = await _service.RequestMealCompletionAsync(traineeId, dto);

        Assert.NotNull(result);
        Assert.False(result.IsCompleted);
        Assert.Equal("PENDING_REVIEW", result.Status);
        Assert.Equal(dto.MediaUrl, result.MediaUrl);
        Assert.Equal(dto.Notes, result.Notes);
        Assert.Equal(1, result.TotalMealsCount);

        _mockPlanRepo.Verify(r => r.AddMealLogAsync(It.Is<MealCompletionLog>(l =>
            l.TraineeId == traineeId &&
            l.MealPlanItemId == item.MealPlanItemId &&
            l.Status == "PENDING_REVIEW" &&
            l.MediaUrl == dto.MediaUrl), It.IsAny<CancellationToken>()), Times.Once);

        _mockNotificationRepo.Verify(n => n.AddAsync(It.Is<Notification>(notif =>
            notif.UserId == coachId &&
            notif.ActorId == traineeId &&
            notif.Type == "MEAL_COMPLETION_REQUESTED"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestMealCompletionAsync_ResubmitUncompleteLog_UpdatesExistingLogAndPreservesNotesWhenNull()
    {
        // UTCID02 - Normal: Resubmitting rejected meal updates existing log and preserves old notes if dto.Notes is null
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var (plan, item) = CreateMealHierarchy(traineeId, coachId);
        var existingLog = new MealCompletionLog
        {
            MealCompletionLogId = Guid.NewGuid(),
            TraineeId = traineeId,
            MealPlanItemId = item.MealPlanItemId,
            Status = "UNCOMPLETE",
            Notes = "Previous note"
        };

        _mockPlanRepo.Setup(r => r.GetMealItemByIdAsync(item.MealPlanItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        _mockPlanRepo.Setup(r => r.GetMealLogAsync(item.MealPlanItemId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLog);
        _mockPlanRepo.Setup(r => r.GetMealPlanByOrderDetailsAsync(plan.OrderDetailsId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var result = await _service.RequestMealCompletionAsync(traineeId, new RequestMealCompletionDto
        {
            MealPlanItemId = item.MealPlanItemId,
            MediaUrl = "  https://cdn.fitsocial.test/retake.jpg  ",
            Notes = null
        });

        Assert.Equal("PENDING_REVIEW", result.Status);
        Assert.Equal(existingLog.MealCompletionLogId, result.MealCompletionLogId);
        Assert.Equal("https://cdn.fitsocial.test/retake.jpg", existingLog.MediaUrl);
        Assert.Equal("Previous note", existingLog.Notes);
        _mockPlanRepo.Verify(r => r.AddMealLogAsync(It.IsAny<MealCompletionLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequestMealCompletionAsync_ScheduledForBusinessToday_Succeeds()
    {
        // UTCID03 - Normal: Meal scheduled for GetBusinessToday() passes date gate and sets LogDate to today
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var today = MealPlanService.GetBusinessToday();
        short todayDay = today.DayOfWeek == DayOfWeek.Sunday ? (short)7 : (short)today.DayOfWeek;
        var (plan, item) = CreateMealHierarchy(traineeId, coachId, planStartDate: today, planEndDate: today.AddDays(30), weekNumber: 1, dayNumber: todayDay);

        _mockPlanRepo.Setup(r => r.GetMealItemByIdAsync(item.MealPlanItemId, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        _mockPlanRepo.Setup(r => r.GetMealLogAsync(item.MealPlanItemId, traineeId, It.IsAny<CancellationToken>())).ReturnsAsync((MealCompletionLog?)null);
        _mockPlanRepo.Setup(r => r.GetMealPlanByOrderDetailsAsync(plan.OrderDetailsId!.Value, It.IsAny<CancellationToken>())).ReturnsAsync(plan);

        var result = await _service.RequestMealCompletionAsync(traineeId, new RequestMealCompletionDto
        {
            MealPlanItemId = item.MealPlanItemId,
            MediaUrl = "https://cdn.fitsocial.test/today_meal.jpg"
        });

        Assert.Equal("PENDING_REVIEW", result.Status);
        _mockPlanRepo.Verify(r => r.AddMealLogAsync(It.Is<MealCompletionLog>(l => l.LogDate.HasValue && l.LogDate.Value.Date == today.Date), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestMealCompletionAsync_ForwardsCancellationTokenAndTrimsFields()
    {
        // UTCID04 - Normal: Trims MediaUrl/Notes and forwards explicit CancellationToken across dependencies
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var (plan, item) = CreateMealHierarchy(traineeId, coachId);

        _mockPlanRepo.Setup(r => r.GetMealItemByIdAsync(item.MealPlanItemId, token)).ReturnsAsync(item);
        _mockPlanRepo.Setup(r => r.GetMealLogAsync(item.MealPlanItemId, traineeId, token)).ReturnsAsync((MealCompletionLog?)null);
        _mockPlanRepo.Setup(r => r.GetMealPlanByOrderDetailsAsync(plan.OrderDetailsId!.Value, token)).ReturnsAsync(plan);

        var result = await _service.RequestMealCompletionAsync(traineeId, new RequestMealCompletionDto
        {
            MealPlanItemId = item.MealPlanItemId,
            MediaUrl = "  https://cdn.fitsocial.test/meal.png  ",
            Notes = "  Tasty!  "
        }, token);

        Assert.Equal("https://cdn.fitsocial.test/meal.png", result.MediaUrl);
        Assert.Equal("Tasty!", result.Notes);
        _mockUow.Verify(u => u.SaveChangesAsync(token), Times.Exactly(2));
        _mockNotificationRepo.Verify(n => n.AddAsync(It.IsAny<Notification>(), token), Times.Once);
    }

    [Fact]
    public async Task RequestMealCompletionAsync_MediaUrl500AndNotes300ExactBoundary_Succeeds()
    {
        // UTCID05 - Boundary: Exact 500-char MediaUrl and 300-char Notes succeed
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var (plan, item) = CreateMealHierarchy(traineeId, coachId);

        _mockPlanRepo.Setup(r => r.GetMealItemByIdAsync(item.MealPlanItemId, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        _mockPlanRepo.Setup(r => r.GetMealLogAsync(item.MealPlanItemId, traineeId, It.IsAny<CancellationToken>())).ReturnsAsync((MealCompletionLog?)null);
        _mockPlanRepo.Setup(r => r.GetMealPlanByOrderDetailsAsync(plan.OrderDetailsId!.Value, It.IsAny<CancellationToken>())).ReturnsAsync(plan);

        var result = await _service.RequestMealCompletionAsync(traineeId, new RequestMealCompletionDto
        {
            MealPlanItemId = item.MealPlanItemId,
            MediaUrl = new string('U', 500),
            Notes = new string('N', 300)
        });

        Assert.Equal("PENDING_REVIEW", result.Status);
        Assert.Equal(500, result.MediaUrl!.Length);
        Assert.Equal(300, result.Notes!.Length);
    }

    [Fact]
    public async Task RequestMealCompletionAsync_MediaUrl501Chars_ThrowsValidationException()
    {
        // UTCID06 - Boundary: 501-char MediaUrl (exceeds 500) -> ValidationException
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.RequestMealCompletionAsync(Guid.NewGuid(), new RequestMealCompletionDto
            {
                MealPlanItemId = Guid.NewGuid(),
                MediaUrl = new string('U', 501)
            }));

        Assert.Equal("MediaUrl cannot exceed 500 characters.", ex.Message);
    }

    [Fact]
    public async Task RequestMealCompletionAsync_Notes301Chars_ThrowsValidationException()
    {
        // UTCID07 - Boundary: 301-char Notes (exceeds 300) -> ValidationException
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.RequestMealCompletionAsync(Guid.NewGuid(), new RequestMealCompletionDto
            {
                MealPlanItemId = Guid.NewGuid(),
                MediaUrl = "https://cdn.fitsocial.test/meal.jpg",
                Notes = new string('N', 301)
            }));

        Assert.Equal("Notes cannot exceed 300 characters.", ex.Message);
    }

    [Fact]
    public async Task RequestMealCompletionAsync_CoachIdEmptyAndRefreshedPlanNull_SkipsNotificationAndReturnsZeroProgress()
    {
        // UTCID08 - Boundary: CoachId == Guid.Empty skips notification; null refreshed plan returns 0 progress safely
        var traineeId = Guid.NewGuid();
        var (plan, item) = CreateMealHierarchy(traineeId, Guid.Empty);

        _mockPlanRepo.Setup(r => r.GetMealItemByIdAsync(item.MealPlanItemId, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        _mockPlanRepo.Setup(r => r.GetMealLogAsync(item.MealPlanItemId, traineeId, It.IsAny<CancellationToken>())).ReturnsAsync((MealCompletionLog?)null);
        _mockPlanRepo.Setup(r => r.GetMealPlanByOrderDetailsAsync(plan.OrderDetailsId!.Value, It.IsAny<CancellationToken>())).ReturnsAsync((MealPlan?)null);

        var result = await _service.RequestMealCompletionAsync(traineeId, new RequestMealCompletionDto
        {
            MealPlanItemId = item.MealPlanItemId,
            MediaUrl = "https://cdn.fitsocial.test/meal.jpg"
        });

        Assert.Equal(0, result.TotalMealsCount);
        Assert.Equal(0, result.CompletedMealsCount);
        Assert.Equal(0.0, result.ProgressPercentage);
        _mockNotificationRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RequestMealCompletionAsync_ScheduledForDifferentDate_ThrowsValidationException()
    {
        // UTCID09 - Boundary: Meal scheduled on a day other than businessToday throws ValidationException
        var traineeId = Guid.NewGuid();
        var today = MealPlanService.GetBusinessToday();
        short todayDay = today.DayOfWeek == DayOfWeek.Sunday ? (short)7 : (short)today.DayOfWeek;
        short otherDay = (short)(todayDay == 1 ? 2 : 1);
        var (_, item) = CreateMealHierarchy(traineeId, planStartDate: today, planEndDate: today.AddDays(30), weekNumber: 1, dayNumber: otherDay);

        _mockPlanRepo.Setup(r => r.GetMealItemByIdAsync(item.MealPlanItemId, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.RequestMealCompletionAsync(traineeId, new RequestMealCompletionDto
            {
                MealPlanItemId = item.MealPlanItemId,
                MediaUrl = "https://cdn.fitsocial.test/meal.jpg"
            }));

        Assert.Contains("You can only complete meals scheduled for today", ex.Message);
    }

    [Fact]
    public async Task RequestMealCompletionAsync_EmptyTraineeId_ThrowsForbiddenException()
    {
        // UTCID10 - Abnormal: Guid.Empty traineeId -> ForbiddenException
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.RequestMealCompletionAsync(Guid.Empty, new RequestMealCompletionDto
            {
                MealPlanItemId = Guid.NewGuid(),
                MediaUrl = "https://cdn.fitsocial.test/meal.jpg"
            }));

        Assert.Equal("Invalid trainee identity.", ex.Message);
    }

    [Fact]
    public async Task RequestMealCompletionAsync_NullDtoOrEmptyItemId_ThrowsValidationException()
    {
        // UTCID11 - Abnormal: Null dto or Guid.Empty MealPlanItemId -> ValidationException
        var traineeId = Guid.NewGuid();

        var exNull = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.RequestMealCompletionAsync(traineeId, null!));
        var exEmpty = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.RequestMealCompletionAsync(traineeId, new RequestMealCompletionDto { MealPlanItemId = Guid.Empty }));

        Assert.Equal("MealPlanItemId is required.", exNull.Message);
        Assert.Equal("MealPlanItemId is required.", exEmpty.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RequestMealCompletionAsync_MissingOrWhitespaceMediaUrl_ThrowsValidationException(string? invalidMedia)
    {
        // UTCID12 - Abnormal: Missing photo proof -> ValidationException
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.RequestMealCompletionAsync(Guid.NewGuid(), new RequestMealCompletionDto
            {
                MealPlanItemId = Guid.NewGuid(),
                MediaUrl = invalidMedia
            }));

        Assert.Equal("Please upload a photo of your meal before submitting for review.", ex.Message);
    }

    [Fact]
    public async Task RequestMealCompletionAsync_MealItemNotFound_ThrowsNotFoundException()
    {
        // UTCID13 - Abnormal: Meal item not found -> NotFoundException
        var itemId = Guid.NewGuid();
        _mockPlanRepo.Setup(r => r.GetMealItemByIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MealPlanItem?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.RequestMealCompletionAsync(Guid.NewGuid(), new RequestMealCompletionDto
            {
                MealPlanItemId = itemId,
                MediaUrl = "https://cdn.fitsocial.test/meal.jpg"
            }));

        Assert.Equal("Meal item not found.", ex.Message);
    }

    [Fact]
    public async Task RequestMealCompletionAsync_UnauthorizedTrainee_ThrowsForbiddenException()
    {
        // UTCID14 - Abnormal: Different trainee attempts to submit meal proof -> ForbiddenException
        var buyerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var (_, item) = CreateMealHierarchy(buyerId);

        _mockPlanRepo.Setup(r => r.GetMealItemByIdAsync(item.MealPlanItemId, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.RequestMealCompletionAsync(attackerId, new RequestMealCompletionDto
            {
                MealPlanItemId = item.MealPlanItemId,
                MediaUrl = "https://cdn.fitsocial.test/meal.jpg"
            }));

        Assert.Equal("You are not authorized to submit completion for this meal.", ex.Message);
    }

    [Fact]
    public async Task RequestMealCompletionAsync_RefundedOrPendingOrder_ThrowsForbiddenException()
    {
        // UTCID15 - Abnormal: Refunded or Pending order blocks meal completion submission
        var traineeId = Guid.NewGuid();
        var (_, refundedItem) = CreateMealHierarchy(traineeId, orderStatus: PaymentConstants.OrderStatusRefunded);

        _mockPlanRepo.Setup(r => r.GetMealItemByIdAsync(refundedItem.MealPlanItemId, It.IsAny<CancellationToken>())).ReturnsAsync(refundedItem);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.RequestMealCompletionAsync(traineeId, new RequestMealCompletionDto
            {
                MealPlanItemId = refundedItem.MealPlanItemId,
                MediaUrl = "https://cdn.fitsocial.test/meal.jpg"
            }));

        Assert.Contains("refunded", ex.Message);
    }

    [Fact]
    public async Task RequestMealCompletionAsync_AlreadyCompletedMeal_ThrowsBusinessException()
    {
        // UTCID16 - Abnormal: Meal already approved (COMPLETED) cannot be submitted again -> BusinessException
        var traineeId = Guid.NewGuid();
        var (_, item) = CreateMealHierarchy(traineeId);
        var completedLog = new MealCompletionLog { MealCompletionLogId = Guid.NewGuid(), TraineeId = traineeId, Status = "COMPLETED" };

        _mockPlanRepo.Setup(r => r.GetMealItemByIdAsync(item.MealPlanItemId, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        _mockPlanRepo.Setup(r => r.GetMealLogAsync(item.MealPlanItemId, traineeId, It.IsAny<CancellationToken>())).ReturnsAsync(completedLog);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.RequestMealCompletionAsync(traineeId, new RequestMealCompletionDto
            {
                MealPlanItemId = item.MealPlanItemId,
                MediaUrl = "https://cdn.fitsocial.test/meal.jpg"
            }));

        Assert.Equal("This meal has already been completed and approved by coach.", ex.Message);
    }

    [Fact]
    public async Task RequestMealCompletionAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // UTCID17 - Abnormal: Cancelled CancellationToken throws OperationCanceledException
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var itemId = Guid.NewGuid();

        _mockPlanRepo.Setup(r => r.GetMealItemByIdAsync(itemId, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.RequestMealCompletionAsync(Guid.NewGuid(), new RequestMealCompletionDto
            {
                MealPlanItemId = itemId,
                MediaUrl = "https://cdn.fitsocial.test/meal.jpg"
            }, cts.Token));
    }

    #endregion

    #region 3. SaveMealMissedReasonAsync (11 Tests: 2 N, 3 B, 6 A)

    [Fact]
    public async Task SaveMealMissedReasonAsync_NoExistingLog_CreatesIncompleteLogWithTrimmedNotes()
    {
        // UTCID01 - Normal: Creates a new INCOMPLETE log with trimmed missed reason
        var traineeId = Guid.NewGuid();
        var today = MealPlanService.GetBusinessToday();
        var (_, item) = CreateMealHierarchy(traineeId, planStartDate: today.AddDays(-7), planEndDate: today.AddDays(21), weekNumber: 1, dayNumber: 1);

        _mockPlanRepo.Setup(r => r.GetMealItemByIdAsync(item.MealPlanItemId, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        _mockPlanRepo.Setup(r => r.GetMealLogAsync(item.MealPlanItemId, traineeId, It.IsAny<CancellationToken>())).ReturnsAsync((MealCompletionLog?)null);

        var result = await _service.SaveMealMissedReasonAsync(traineeId, new SaveMealMissedReasonDto
        {
            MealPlanItemId = item.MealPlanItemId,
            Notes = "  Busy with overtime at work  "
        });

        Assert.NotNull(result);
        Assert.False(result.IsCompleted);
        Assert.Equal("INCOMPLETE", result.Status);
        Assert.Equal("Busy with overtime at work", result.Notes);
        _mockPlanRepo.Verify(r => r.AddMealLogAsync(It.Is<MealCompletionLog>(l =>
            l.TraineeId == traineeId &&
            l.Status == "INCOMPLETE" &&
            l.Notes == "Busy with overtime at work"), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveMealMissedReasonAsync_ExistingIncompleteLog_UpdatesStatusAndNotes()
    {
        // UTCID02 - Normal: Updates existing INCOMPLETE/UNCOMPLETE log notes instead of inserting duplicate
        var traineeId = Guid.NewGuid();
        var (_, item) = CreateMealHierarchy(traineeId);
        var existingLog = new MealCompletionLog
        {
            MealCompletionLogId = Guid.NewGuid(),
            TraineeId = traineeId,
            MealPlanItemId = item.MealPlanItemId,
            Status = "INCOMPLETE",
            Notes = null
        };

        _mockPlanRepo.Setup(r => r.GetMealItemByIdAsync(item.MealPlanItemId, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        _mockPlanRepo.Setup(r => r.GetMealLogAsync(item.MealPlanItemId, traineeId, It.IsAny<CancellationToken>())).ReturnsAsync(existingLog);

        var result = await _service.SaveMealMissedReasonAsync(traineeId, new SaveMealMissedReasonDto
        {
            MealPlanItemId = item.MealPlanItemId,
            Notes = "Had a stomachache"
        });

        Assert.Equal(existingLog.MealCompletionLogId, result.MealCompletionLogId);
        Assert.Equal("INCOMPLETE", existingLog.Status);
        Assert.Equal("Had a stomachache", existingLog.Notes);
        _mockPlanRepo.Verify(r => r.AddMealLogAsync(It.IsAny<MealCompletionLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveMealMissedReasonAsync_NotesExactly300Chars_Succeeds()
    {
        // UTCID03 - Boundary: Exact 300-char missed reason succeeds
        var traineeId = Guid.NewGuid();
        var (_, item) = CreateMealHierarchy(traineeId);
        var maxNotes = new string('R', 300);

        _mockPlanRepo.Setup(r => r.GetMealItemByIdAsync(item.MealPlanItemId, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        _mockPlanRepo.Setup(r => r.GetMealLogAsync(item.MealPlanItemId, traineeId, It.IsAny<CancellationToken>())).ReturnsAsync((MealCompletionLog?)null);

        var result = await _service.SaveMealMissedReasonAsync(traineeId, new SaveMealMissedReasonDto
        {
            MealPlanItemId = item.MealPlanItemId,
            Notes = maxNotes
        });

        Assert.Equal("INCOMPLETE", result.Status);
        Assert.Equal(300, result.Notes!.Length);
    }

    [Fact]
    public async Task SaveMealMissedReasonAsync_Notes301Chars_ThrowsValidationException()
    {
        // UTCID04 - Boundary: 301-char missed reason throws ValidationException
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SaveMealMissedReasonAsync(Guid.NewGuid(), new SaveMealMissedReasonDto
            {
                MealPlanItemId = Guid.NewGuid(),
                Notes = new string('R', 301)
            }));

        Assert.Equal("Notes cannot exceed 300 characters.", ex.Message);
    }

    [Fact]
    public async Task SaveMealMissedReasonAsync_FlexiblePlanWithoutStartDate_UsesCurrentUtcAsLogDate()
    {
        // UTCID05 - Boundary: Plan without StartDate falls back to current UTC time for LogDate
        var traineeId = Guid.NewGuid();
        var (_, item) = CreateMealHierarchy(traineeId, planStartDate: null, planEndDate: null);

        _mockPlanRepo.Setup(r => r.GetMealItemByIdAsync(item.MealPlanItemId, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        _mockPlanRepo.Setup(r => r.GetMealLogAsync(item.MealPlanItemId, traineeId, It.IsAny<CancellationToken>())).ReturnsAsync((MealCompletionLog?)null);

        var result = await _service.SaveMealMissedReasonAsync(traineeId, new SaveMealMissedReasonDto
        {
            MealPlanItemId = item.MealPlanItemId,
            Notes = "Skipped due to travel"
        });

        Assert.Equal("INCOMPLETE", result.Status);
        _mockPlanRepo.Verify(r => r.AddMealLogAsync(It.Is<MealCompletionLog>(l => l.LogDate.HasValue && l.LogDate.Value.Date == DateTime.UtcNow.Date), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveMealMissedReasonAsync_EmptyTraineeId_ThrowsForbiddenException()
    {
        // UTCID06 - Abnormal: Guid.Empty traineeId -> ForbiddenException
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.SaveMealMissedReasonAsync(Guid.Empty, new SaveMealMissedReasonDto
            {
                MealPlanItemId = Guid.NewGuid(),
                Notes = "Forgot"
            }));

        Assert.Equal("Invalid trainee identity.", ex.Message);
    }

    [Fact]
    public async Task SaveMealMissedReasonAsync_NullDtoOrEmptyItemId_ThrowsValidationException()
    {
        // UTCID07 - Abnormal: Null dto or Guid.Empty MealPlanItemId -> ValidationException
        var traineeId = Guid.NewGuid();

        var exNull = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SaveMealMissedReasonAsync(traineeId, null!));
        var exEmpty = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SaveMealMissedReasonAsync(traineeId, new SaveMealMissedReasonDto { MealPlanItemId = Guid.Empty, Notes = "Forgot" }));

        Assert.Equal("MealPlanItemId is required.", exNull.Message);
        Assert.Equal("MealPlanItemId is required.", exEmpty.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveMealMissedReasonAsync_MissingOrWhitespaceNotes_ThrowsValidationException(string? invalidNotes)
    {
        // UTCID08 - Abnormal: Missing missed reason -> ValidationException
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SaveMealMissedReasonAsync(Guid.NewGuid(), new SaveMealMissedReasonDto
            {
                MealPlanItemId = Guid.NewGuid(),
                Notes = invalidNotes
            }));

        Assert.Equal("Please enter the reason for missing this meal.", ex.Message);
    }

    [Fact]
    public async Task SaveMealMissedReasonAsync_MealItemNotFound_ThrowsNotFoundException()
    {
        // UTCID09 - Abnormal: Missing meal item -> NotFoundException
        var itemId = Guid.NewGuid();
        _mockPlanRepo.Setup(r => r.GetMealItemByIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MealPlanItem?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.SaveMealMissedReasonAsync(Guid.NewGuid(), new SaveMealMissedReasonDto
            {
                MealPlanItemId = itemId,
                Notes = "Travelling"
            }));

        Assert.Equal("Meal item not found.", ex.Message);
    }

    [Fact]
    public async Task SaveMealMissedReasonAsync_UnauthorizedTraineeOrRefundedOrder_ThrowsForbiddenException()
    {
        // UTCID10 - Abnormal: Unauthorized trainee or refunded order blocks saving missed reason
        var buyerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var (_, item) = CreateMealHierarchy(buyerId);
        var (_, refundedItem) = CreateMealHierarchy(buyerId, orderStatus: PaymentConstants.OrderStatusRefunded);

        _mockPlanRepo.Setup(r => r.GetMealItemByIdAsync(item.MealPlanItemId, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        _mockPlanRepo.Setup(r => r.GetMealItemByIdAsync(refundedItem.MealPlanItemId, It.IsAny<CancellationToken>())).ReturnsAsync(refundedItem);

        var exUnauth = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.SaveMealMissedReasonAsync(attackerId, new SaveMealMissedReasonDto { MealPlanItemId = item.MealPlanItemId, Notes = "Busy" }));
        var exRefunded = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.SaveMealMissedReasonAsync(buyerId, new SaveMealMissedReasonDto { MealPlanItemId = refundedItem.MealPlanItemId, Notes = "Busy" }));

        Assert.Equal("You are not authorized to update this meal.", exUnauth.Message);
        Assert.Contains("refunded", exRefunded.Message);
    }

    [Fact]
    public async Task SaveMealMissedReasonAsync_AlreadyCompletedMeal_ThrowsValidationException()
    {
        // UTCID11 - Abnormal: Already completed meal cannot be marked incomplete -> ValidationException
        var traineeId = Guid.NewGuid();
        var (_, item) = CreateMealHierarchy(traineeId);
        var completedLog = new MealCompletionLog { MealCompletionLogId = Guid.NewGuid(), TraineeId = traineeId, Status = "COMPLETED" };

        _mockPlanRepo.Setup(r => r.GetMealItemByIdAsync(item.MealPlanItemId, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        _mockPlanRepo.Setup(r => r.GetMealLogAsync(item.MealPlanItemId, traineeId, It.IsAny<CancellationToken>())).ReturnsAsync(completedLog);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SaveMealMissedReasonAsync(traineeId, new SaveMealMissedReasonDto
            {
                MealPlanItemId = item.MealPlanItemId,
                Notes = "Trying to overwrite"
            }));

        Assert.Equal("Completed meals cannot be marked incomplete.", ex.Message);
    }

    #endregion
}
