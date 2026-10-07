using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        var grouped = orders
            .GroupBy(o => o.BuyerId)
            .Select(group =>
            {
                var buyer = group.First().Buyer;
                var traineeName = buyer?.FullName ?? buyer?.Email ?? "Trainee";
                var traineeEmail = buyer?.Email ?? string.Empty;
                var traineePhone = buyer?.PhoneNumber;
                var traineeAvatar = buyer?.AvatarUrl;

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
                        var thumbnailUrl = pkg?.Media?.OrderBy(m => m.SortOrder).Select(m => m.MediaUrl).FirstOrDefault();

                        packages.Add(new CoachTraineePackageOrderDto
                        {
                            OrderId = order.OrderId,
                            OrderDetailsId = detail.OrderDetailsId,
                            PackageId = detail.PackageId,
                            PackageTitle = detail.PackageTitle ?? pkg?.Title ?? "Training Package",
                            PackageDescription = pkg?.Description,
                            DurationDays = detail.PackageDurationDays ?? pkg?.DurationDays,
                            SessionCount = pkg?.SessionCount,
                            PricePaid = detail.PackagePrice ?? order.TotalAmount,
                            OrderStatus = order.OrderStatus,
                            OrderCode = paymentCode,
                            PurchasedAt = order.CreatedAt,
                            ThumbnailUrl = thumbnailUrl,
                            TrainingPlanId = plan?.TrainingPlanId,
                            PlanStatus = plan?.Status ?? (order.OrderStatus == "COMPLETED" ? "ACTIVE" : order.OrderStatus)
                        });
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

        return ApiResponseDto<List<CoachTraineeGroupDto>>.Ok(grouped, "Trainee orders retrieved successfully.");
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
                CreatedAt = d.CreatedAt
            }).ToList()
        };
    }
}
