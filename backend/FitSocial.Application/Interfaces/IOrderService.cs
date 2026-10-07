using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Orders;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Application.Interfaces;

public interface IOrderService
{
    Task<ApiResponseDto<OrderDto>> CreatePackageOrderAsync(
        Guid buyerId,
        CreatePackageOrderRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<List<OrderDto>>> GetMyOrdersAsync(
        Guid buyerId,
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<OrderDto>> GetOrderByIdAsync(
        Guid orderId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);
<<<<<<< Updated upstream

    Task<ApiResponseDto<List<CoachTraineeGroupDto>>> GetCoachTraineeOrdersAsync(
        Guid coachId,
        CancellationToken cancellationToken = default);
=======
  
>>>>>>> Stashed changes
}
