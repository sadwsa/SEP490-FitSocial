using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Orders;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Application.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly ITrainingPackageRepository _packageRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public OrderService(
        IOrderRepository orderRepository,
        ITrainingPackageRepository packageRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _packageRepository = packageRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponseDto<OrderDto>> CreatePackageOrderAsync(
        Guid buyerId,
        CreatePackageOrderRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (buyerId == Guid.Empty)
        {
            throw new ValidationException("User is not authenticated.");
        }

        if (request == null || request.PackageId == Guid.Empty)
        {
            throw new ValidationException("Package ID is required.");
        }

        var buyer = await _userRepository.GetByIdAsync(buyerId, cancellationToken);
        if (buyer == null)
        {
            throw new NotFoundException("Buyer account not found.");
        }

        var package = await _packageRepository.GetByIdAsync(request.PackageId, cancellationToken);
        if (package == null)
        {
            throw new NotFoundException("Training package not found.");
        }

        if (package.IsActive != true)
        {
            throw new BusinessException("Training package is no longer available.");
        }

        if (package.CoachId == buyerId)
        {
            throw new BusinessException("You cannot purchase your own package.");
        }

        var now = DateTime.UtcNow;
        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            BuyerId = buyerId,
            CoachId = package.CoachId,
            TotalAmount = package.Price ?? 0,
            OrderStatus = "PENDING",
            OrderType = "PACKAGE",
            CreatedAt = now
        };

        var coachName = package.Coach?.Coach?.FullName;
        if (string.IsNullOrWhiteSpace(coachName))
        {
            var coachUser = await _userRepository.GetByIdAsync(package.CoachId, cancellationToken);
            coachName = coachUser?.FullName ?? coachUser?.Email ?? "Coach";
        }

        var orderDetail = new OrderDetail
        {
            OrderDetailsId = Guid.NewGuid(),
            OrderId = order.OrderId,
            PackageId = package.PackageId,
            PackagePrice = package.Price,
            PackageTitle = package.Title ?? "Training Package",
            PackageDurationDays = package.DurationDays,
            PackageType = package.PackageType ?? "BOTH",
            CoachName = coachName,
            CreatedAt = now
        };

        order.OrderDetails.Add(orderDetail);

        await _orderRepository.AddAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var detailedOrder = await _orderRepository.GetOrderByIdWithDetailsAsync(order.OrderId, cancellationToken);
        return ApiResponseDto<OrderDto>.Ok(MapToOrderDto(detailedOrder ?? order), "Order created successfully.");
    }

    public async Task<ApiResponseDto<List<OrderDto>>> GetMyOrdersAsync(
        Guid buyerId,
        CancellationToken cancellationToken = default)
    {
        var orders = await _orderRepository.GetOrdersByBuyerIdAsync(buyerId, cancellationToken);
        var dtos = orders.Select(MapToOrderDto).ToList();
        return ApiResponseDto<List<OrderDto>>.Ok(dtos, "Orders retrieved successfully.");
    }

    public async Task<ApiResponseDto<OrderDto>> GetOrderByIdAsync(
        Guid orderId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetOrderByIdWithDetailsAsync(orderId, cancellationToken);
        if (order == null)
        {
            throw new NotFoundException("Order not found.");
        }

        // Authorization: only the buyer or coach or admin can view the order
        if (order.BuyerId != currentUserId && order.CoachId != currentUserId)
        {
            throw new ForbiddenException("You do not have access to view this order.");
        }

        return ApiResponseDto<OrderDto>.Ok(MapToOrderDto(order));
    }

    public async Task<ApiResponseDto<List<CoachTraineeGroupDto>>> GetCoachTraineeOrdersAsync(
        Guid coachId,
        CancellationToken cancellationToken = default)
    {
        if (coachId == Guid.Empty)
        {
            throw new ValidationException("Invalid coach identity.");
        }

        var orders = await _orderRepository.GetOrdersByCoachIdAsync(coachId, cancellationToken);

        var grouped = BuildTraineeGroups(orders, coachId);

        return ApiResponseDto<List<CoachTraineeGroupDto>>.Ok(grouped, "Trainee orders retrieved successfully.");
    }

    public async Task<ApiResponseDto<CoachTraineeGroupDto>> GetCoachTraineeOrderDetailAsync(
        Guid coachId,
        Guid traineeId,
        CancellationToken cancellationToken = default)
    {
        if (coachId == Guid.Empty)
        {
            throw new ValidationException("Invalid coach identity.");
        }

        if (traineeId == Guid.Empty)
        {
            throw new ValidationException("Invalid trainee identity.");
        }

        var orders = await _orderRepository.GetOrdersByCoachIdAsync(coachId, cancellationToken);

        var group = BuildTraineeGroups(orders, coachId)
            .FirstOrDefault(g => g.TraineeId == traineeId);

        if (group == null)
        {
            throw new NotFoundException("No trainee orders found for this trainee.");
        }

        return ApiResponseDto<CoachTraineeGroupDto>.Ok(group, "Trainee order details retrieved successfully.");
    }

    /// <summary>
    /// Gom đơn hàng của coach theo từng trainee và dựng DTO cho mỗi nhóm.
    /// Dùng chung cho API danh sách và API chi tiết một trainee để không lặp logic map.
    /// </summary>
    private static List<CoachTraineeGroupDto> BuildTraineeGroups(IEnumerable<Order> orders, Guid coachId)
    {
        return orders
            .GroupBy(o => o.BuyerId)
            .Select(group =>
            {
                var buyer = group.First().Buyer;
                var traineeName = buyer?.FullName ?? buyer?.Email ?? "Trainee";
                var traineeEmail = buyer?.Email ?? string.Empty;
                var traineePhone = buyer?.PhoneNumber;
                var traineeAvatar = buyer?.AvatarUrl;

                var today = DateTime.UtcNow.Date;
                var packages = new List<CoachTraineePackageOrderDto>();

                foreach (var order in group)
                {
                    var paymentCode = order.Payments?.FirstOrDefault()?.GatewayTransactionId;

                    foreach (var detail in order.OrderDetails)
                    {
                        var pkg = detail.Package;
                        if (pkg != null && pkg.CoachId != coachId && order.CoachId != coachId)
                        {
                            continue;
                        }

                        var plan = detail.TrainingPlans?.FirstOrDefault();
                        var mealPlan = detail.MealPlans?.FirstOrDefault();
                        var thumbnailUrl = pkg?.Media?.OrderBy(m => m.SortOrder).Select(m => m.MediaUrl).FirstOrDefault();

                        var pkgDto = new CoachTraineePackageOrderDto
                        {
                            OrderId = order.OrderId,
                            OrderDetailsId = detail.OrderDetailsId,
                            PackageId = detail.PackageId,
                            PackageTitle = detail.PackageTitle ?? pkg?.Title ?? "Training Package",
                            PackageDescription = pkg?.Description,
                            PackageType = detail.PackageType ?? pkg?.PackageType ?? "BOTH",
                            DurationDays = detail.PackageDurationDays ?? pkg?.DurationDays,
                            SessionCount = pkg?.SessionCount,
                            PricePaid = detail.PackagePrice ?? order.TotalAmount,
                            OrderStatus = order.OrderStatus,
                            OrderCode = paymentCode,
                            PurchasedAt = order.CreatedAt,
                            ThumbnailUrl = thumbnailUrl,
                            TrainingPlanId = plan?.TrainingPlanId,
                            PlanStatus = plan?.Status,
                            MealPlanId = mealPlan?.MealPlanId,
                            MealPlanStatus = mealPlan?.Status
                        };

                        ComputeScheduleHealth(pkgDto, plan, mealPlan, today);
                        packages.Add(pkgDto);
                    }
                }

                var totalSpent = packages.Sum(p => p.PricePaid ?? 0);
                var latestDate = packages.Count > 0 ? packages.Max(p => p.PurchasedAt) : null;

                return new CoachTraineeGroupDto
                {
                    TraineeId = group.Key,
                    FullName = traineeName,
                    Email = traineeEmail,
                    PhoneNumber = traineePhone,
                    AvatarUrl = traineeAvatar,
                    TotalOrdersCount = packages.Count,
                    TotalSpent = totalSpent,
                    LatestPurchasedAt = latestDate,
                    PurchasedPackages = packages.OrderByDescending(p => p.PurchasedAt).ToList()
                };
            })
            .Where(g => g.PurchasedPackages.Count > 0)
            .OrderByDescending(g => g.LatestPurchasedAt)
            .ToList();
    }

    private static void ComputeScheduleHealth(
        CoachTraineePackageOrderDto dto,
        TrainingPlan? plan,
        MealPlan? mealPlan,
        DateTime today)
    {
        // 1. Workout Schedule Health
        if (dto.PackageType != "MEAL")
        {
            if (plan == null)
            {
                dto.WorkoutScheduleStatus = "NO_PLAN";
                dto.WorkoutScheduleStatusText = "Chưa tạo giáo án";
            }
            else if (string.Equals(plan.Status, "DRAFT", StringComparison.OrdinalIgnoreCase))
            {
                dto.WorkoutScheduleStatus = "NO_PLAN";
                dto.WorkoutScheduleStatusText = "Bản nháp (DRAFT)";
            }
            else if (plan.StartDate.HasValue && plan.EndDate.HasValue)
            {
                var startDate = plan.StartDate.Value.Date;
                var endDate = plan.EndDate.Value.Date;
                int remainingDays = Math.Max(0, (endDate - today).Days);
                dto.RemainingDays = remainingDays;

                var scheduledDates = plan.TrainingPlanExercises
                    .Where(e => !string.IsNullOrWhiteSpace(e.ExerciseName))
                    .Select(e => TrainingPlanService.GetSessionDate(startDate, e.WeekNumber ?? 1, e.DayNumber ?? 1).Date)
                    .Distinct()
                    .OrderBy(d => d)
                    .ToList();

                var maxSessions = dto.SessionCount;
                if (maxSessions.HasValue && maxSessions.Value > 0)
                {
                    int remainingSessions = maxSessions.Value - scheduledDates.Count;
                    dto.RemainingSessions = remainingSessions;

                    if (remainingSessions <= 0)
                    {
                        dto.WorkoutScheduleStatus = "COMPLETED";
                        dto.WorkoutScheduleStatusText = $"Đã lên đủ {scheduledDates.Count}/{maxSessions.Value} buổi";
                    }
                    else
                    {
                        var upcomingSessions = scheduledDates.Where(d => d >= today).ToList();
                        dto.NextWorkoutDate = upcomingSessions.FirstOrDefault();

                        if (upcomingSessions.Count == 0)
                        {
                            dto.WorkoutScheduleStatus = "URGENT";
                            dto.WorkoutScheduleStatusText = $"Còn {remainingSessions} buổi - Chưa có lịch tiếp theo";
                        }
                        else if (upcomingSessions.Count == 1)
                        {
                            var next = upcomingSessions[0];
                            int daysUntil = (next - today).Days;
                            if (daysUntil <= 1)
                            {
                                dto.WorkoutScheduleStatus = "URGENT";
                                dto.WorkoutScheduleStatusText = $"Còn {remainingSessions} buổi - Hết bài sau {next:dd/MM}";
                            }
                            else if (daysUntil <= 3)
                            {
                                dto.WorkoutScheduleStatus = "WARNING";
                                dto.WorkoutScheduleStatusText = $"Còn {remainingSessions} buổi - Buổi tới: {next:dd/MM}";
                            }
                            else
                            {
                                dto.WorkoutScheduleStatus = "SAFE";
                                dto.WorkoutScheduleStatusText = $"Còn {remainingSessions} buổi - Buổi tới: {next:dd/MM}";
                            }
                        }
                        else
                        {
                            dto.WorkoutScheduleStatus = "SAFE";
                            dto.WorkoutScheduleStatusText = $"Còn {remainingSessions} buổi - Buổi tới: {upcomingSessions[0]:dd/MM}";
                        }
                    }
                }
                else // Flexible package (SessionCount == null)
                {
                    if (remainingDays <= 0)
                    {
                        dto.WorkoutScheduleStatus = "COMPLETED";
                        dto.WorkoutScheduleStatusText = "Gói tập đã hết hạn";
                    }
                    else
                    {
                        var upcomingSessions = scheduledDates.Where(d => d >= today).ToList();
                        dto.NextWorkoutDate = upcomingSessions.FirstOrDefault();

                        if (upcomingSessions.Count == 0)
                        {
                            dto.WorkoutScheduleStatus = "URGENT";
                            dto.WorkoutScheduleStatusText = $"Còn {remainingDays} ngày - Chưa có lịch tiếp theo";
                        }
                        else if (upcomingSessions.Count == 1)
                        {
                            var next = upcomingSessions[0];
                            int daysUntil = (next - today).Days;
                            if (daysUntil <= 1)
                            {
                                dto.WorkoutScheduleStatus = "URGENT";
                                dto.WorkoutScheduleStatusText = $"Còn {remainingDays} ngày - Hết bài sau {next:dd/MM}";
                            }
                            else if (daysUntil <= 3)
                            {
                                dto.WorkoutScheduleStatus = "WARNING";
                                dto.WorkoutScheduleStatusText = $"Còn {remainingDays} ngày - Buổi tới: {next:dd/MM}";
                            }
                            else
                            {
                                dto.WorkoutScheduleStatus = "SAFE";
                                dto.WorkoutScheduleStatusText = $"Còn {remainingDays} ngày - Buổi tới: {next:dd/MM}";
                            }
                        }
                        else
                        {
                            dto.WorkoutScheduleStatus = "SAFE";
                            dto.WorkoutScheduleStatusText = $"Còn {remainingDays} ngày - Buổi tới: {upcomingSessions[0]:dd/MM}";
                        }
                    }
                }
            }
        }

        // 2. Meal Schedule Health
        if (dto.PackageType != "WORKOUT")
        {
            if (mealPlan == null)
            {
                dto.MealScheduleStatus = "NO_PLAN";
                dto.MealScheduleStatusText = "Chưa tạo thực đơn";
            }
            else if (string.Equals(mealPlan.Status, "DRAFT", StringComparison.OrdinalIgnoreCase))
            {
                dto.MealScheduleStatus = "NO_PLAN";
                dto.MealScheduleStatusText = "Bản nháp (DRAFT)";
            }
            else if (mealPlan.StartDate.HasValue && mealPlan.EndDate.HasValue)
            {
                var startDate = mealPlan.StartDate.Value.Date;
                var endDate = mealPlan.EndDate.Value.Date;
                var twoDaysAhead = today.AddDays(2);

                if (twoDaysAhead > endDate)
                {
                    dto.MealScheduleStatus = "SAFE";
                    dto.MealScheduleStatusText = "Thực đơn hoàn tất";
                }
                else
                {
                    bool hasMealTwoDaysAhead = mealPlan.MealPlanItems.Any(i =>
                        !string.IsNullOrWhiteSpace(i.FoodDescription) &&
                        MealPlanService.GetSessionDate(startDate, i.WeekNumber ?? 1, i.DayNumber ?? 1).Date == twoDaysAhead);

                    if (hasMealTwoDaysAhead)
                    {
                        dto.MealScheduleStatus = "SAFE";
                        dto.MealScheduleStatusText = "Thực đơn đầy đủ (>= 2 ngày)";
                    }
                    else
                    {
                        dto.MealScheduleStatus = "WARNING";
                        dto.MealScheduleStatusText = $"Thiếu thực đơn ngày {twoDaysAhead:dd/MM}";
                    }
                }
            }
        }
    }

    private static OrderDto MapToOrderDto(Order o)
    {
        return new OrderDto
        {
            OrderId = o.OrderId,
            BuyerId = o.BuyerId,
            BuyerName = o.Buyer?.FullName ?? o.Buyer?.Email,
            BuyerEmail = o.Buyer?.Email,
            CoachId = o.CoachId,
            CoachName = o.Coach?.Coach?.FullName ?? o.OrderDetails.FirstOrDefault()?.CoachName,
            TotalAmount = o.TotalAmount,
            OrderStatus = o.OrderStatus,
            OrderType = o.OrderType,
            CreatedAt = o.CreatedAt,
            Details = o.OrderDetails.Select(d => new OrderDetailDto
            {
                OrderDetailsId = d.OrderDetailsId,
                OrderId = d.OrderId,
                PackageId = d.PackageId,
                CoachSubscriptionPlansId = d.CoachSubscriptionPlansId,
                PackageTitle = d.PackageTitle ?? d.Package?.Title,
                PackageDurationDays = d.PackageDurationDays ?? d.Package?.DurationDays,
                CoachName = d.CoachName,
                PackagePrice = d.PackagePrice,
                PackageType = d.PackageType,
                CreatedAt = d.CreatedAt
            }).ToList()
        };
    }
}
