using FitSocial.Application.DTOs.Cart;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Application.Services
{
    public class CartService : ICartService
    {
        private readonly ICartRepository _cartRepository;
        private readonly ITrainingPackageRepository _packageRepository;
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CartService(
            ICartRepository cartRepository,
            ITrainingPackageRepository packageRepository,
            IUserRepository userRepository,
            IUnitOfWork unitOfWork)
        {
            _cartRepository = cartRepository;
            _packageRepository = packageRepository;
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<ApiResponseDto<CartSummaryDto>> GetCartAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
                var isCoach = string.Equals(user?.RoleCode, RoleConstants.Coach, StringComparison.OrdinalIgnoreCase);

                var cartEntities = await _cartRepository.GetCartByUserIdAsync(userId, cancellationToken);
                var itemDtos = new List<CartItemDto>();

                foreach (var c in cartEntities)
                {
                    if (c.Package == null) continue;

                    var pkg = c.Package;
                    var coachProfile = pkg.Coach;
                    var coachUser = coachProfile?.Coach;
                    var sports = coachProfile?.Sports?.ToList();
                    var reviews = coachProfile?.Reviews?.ToList();

                    var isOwnPackage = (pkg.CoachId == userId);
                    var isPackageActive = pkg.IsActive ?? true;
                    var isAvailable = isPackageActive && !isOwnPackage;
                    string? unavailableReason = null;

                    if (isOwnPackage)
                    {
                        unavailableReason = "Gói tập của chính bạn - Không thể mua";
                    }
                    else if (!isPackageActive)
                    {
                        unavailableReason = "Gói tập này hiện đã tạm dừng nhận học viên mới bởi HLV";
                    }

                    var durationDays = pkg.DurationDays ?? 30;
                    string durationLabel;
                    if (durationDays >= 7 && durationDays % 7 == 0)
                    {
                        durationLabel = $"{durationDays / 7} Weeks ({durationDays} Days)";
                    }
                    else
                    {
                        durationLabel = $"{durationDays} Days";
                    }

                    double avgRating = 4.9;
                    int reviewCount = 0;
                    if (reviews != null && reviews.Count > 0)
                    {
                        reviewCount = reviews.Count;
                        var validRatings = reviews.Where(r => r.Rating.HasValue).Select(r => r.Rating!.Value).ToList();
                        if (validRatings.Count > 0)
                        {
                            avgRating = Math.Round(validRatings.Average(), 1);
                        }
                    }

                    var sportName = sports != null && sports.Count > 0 ? sports[0].SportName : "Gym & Strength";

                    itemDtos.Add(new CartItemDto
                    {
                        CartId = c.CartId,
                        PackageId = pkg.PackageId,
                        Title = pkg.Title ?? "Custom Training Package",
                        DurationDays = pkg.DurationDays,
                        DurationLabel = durationLabel,
                        Price = pkg.Price ?? 0,
                        Quantity = c.Quantity <= 0 ? 1 : c.Quantity,
                        CoachId = pkg.CoachId,
                        CoachName = coachUser?.FullName ?? "Coach",
                        CoachAvatarUrl = coachUser?.AvatarUrl,
                        SportName = sportName,
                        ExperienceYears = coachProfile?.ExperienceYears ?? 5,
                        AverageRating = avgRating,
                        ReviewCount = reviewCount,
                        IsActive = isPackageActive,
                        IsAvailable = isAvailable,
                        UnavailableReason = unavailableReason,
                        CreatedAt = c.CreatedAt
                    });
                }

                var validItems = itemDtos.Where(i => i.IsAvailable).ToList();
                var subtotal = validItems.Sum(i => i.Price * i.Quantity);
                var platformFee = 0m;
                var total = subtotal + platformFee;
                var approxUsd = Math.Round(total / 25000m, 2);

                var orderCodeSuffix = Math.Abs(userId.GetHashCode()) % 90000 + 10000;
                var orderCode = $"FS-{orderCodeSuffix}";

                var summary = new CartSummaryDto
                {
                    Items = itemDtos,
                    TotalItems = itemDtos.Count,
                    ValidItemsCount = validItems.Count,
                    Subtotal = subtotal,
                    PlatformFee = platformFee,
                    TotalPrice = total,
                    ApproxUsd = approxUsd,
                    HasUnavailableItems = itemDtos.Any(i => !i.IsAvailable),
                    IsCoach = isCoach,
                    TemporaryOrderCode = orderCode
                };

                return ApiResponseDto<CartSummaryDto>.Ok(summary, "Lấy giỏ hàng thành công.");
            }
            catch (Exception ex)
            {
                return ApiResponseDto<CartSummaryDto>.Fail($"Lỗi khi tải giỏ hàng: {ex.Message}");
            }
        }

        public async Task<ApiResponseDto<int>> GetCartCountAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var count = await _cartRepository.GetCartCountByUserIdAsync(userId, cancellationToken);
                return ApiResponseDto<int>.Ok(count);
            }
            catch (Exception ex)
            {
                return ApiResponseDto<int>.Fail($"Lỗi khi đếm số lượng giỏ hàng: {ex.Message}");
            }
        }

        public async Task<ApiResponseDto<CartItemDto>> AddToCartAsync(Guid userId, AddToCartDto dto, CancellationToken cancellationToken = default)
        {
            if (dto == null || dto.PackageId == Guid.Empty)
            {
                return ApiResponseDto<CartItemDto>.Fail("Thông tin gói tập không hợp lệ.");
            }

            try
            {
                var package = await _packageRepository.GetByIdAsync(dto.PackageId, cancellationToken);
                if (package == null)
                {
                    return ApiResponseDto<CartItemDto>.Fail("Gói tập không tồn tại.");
                }

                if (package.CoachId == userId)
                {
                    return ApiResponseDto<CartItemDto>.Fail("Bạn không thể thêm gói tập do chính mình tạo vào giỏ hàng.");
                }

                if (package.IsActive == false)
                {
                    return ApiResponseDto<CartItemDto>.Fail("Gói tập này hiện đang tạm dừng nhận học viên.");
                }

                var existing = await _cartRepository.GetCartItemAsync(userId, dto.PackageId, cancellationToken);
                if (existing != null)
                {
                    var existingDto = new CartItemDto
                    {
                        CartId = existing.CartId,
                        PackageId = existing.PackageId,
                        Title = package.Title ?? "Training Package",
                        Price = package.Price ?? 0,
                        Quantity = existing.Quantity,
                        CoachId = package.CoachId,
                        CoachName = package.Coach?.Coach?.FullName ?? "Coach",
                        CoachAvatarUrl = package.Coach?.Coach?.AvatarUrl,
                        IsActive = package.IsActive ?? true,
                        IsAvailable = true
                    };
                    return ApiResponseDto<CartItemDto>.Ok(existingDto, "Gói tập đã có sẵn trong giỏ hàng của bạn.");
                }

                var newCart = new Cart
                {
                    CartId = Guid.NewGuid(),
                    UserId = userId,
                    PackageId = dto.PackageId,
                    Quantity = dto.Quantity > 0 ? dto.Quantity : 1,
                    CreatedAt = DateTime.UtcNow
                };

                await _cartRepository.AddAsync(newCart, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var resultDto = new CartItemDto
                {
                    CartId = newCart.CartId,
                    PackageId = newCart.PackageId,
                    Title = package.Title ?? "Training Package",
                    Price = package.Price ?? 0,
                    Quantity = newCart.Quantity,
                    CoachId = package.CoachId,
                    CoachName = package.Coach?.Coach?.FullName ?? "Coach",
                    CoachAvatarUrl = package.Coach?.Coach?.AvatarUrl,
                    IsActive = true,
                    IsAvailable = true,
                    CreatedAt = newCart.CreatedAt
                };

                return ApiResponseDto<CartItemDto>.Ok(resultDto, "Đã thêm gói tập vào giỏ hàng thành công.");
            }
            catch (Exception ex)
            {
                return ApiResponseDto<CartItemDto>.Fail($"Lỗi khi thêm vào giỏ hàng: {ex.Message}");
            }
        }

        public async Task<ApiResponseDto<bool>> RemoveCartItemAsync(Guid userId, Guid cartId, CancellationToken cancellationToken = default)
        {
            try
            {
                var cart = await _cartRepository.GetByIdAsync(cartId, cancellationToken);
                if (cart == null || cart.UserId != userId)
                {
                    return ApiResponseDto<bool>.Fail("Mục trong giỏ hàng không tồn tại hoặc không thuộc về bạn.");
                }

                _cartRepository.Remove(cart);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponseDto<bool>.Ok(true, "Đã xóa gói tập khỏi giỏ hàng.");
            }
            catch (Exception ex)
            {
                return ApiResponseDto<bool>.Fail($"Lỗi khi xóa gói tập khỏi giỏ hàng: {ex.Message}");
            }
        }

        public async Task<ApiResponseDto<bool>> ClearCartAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                await _cartRepository.ClearCartAsync(userId, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponseDto<bool>.Ok(true, "Đã làm trống giỏ hàng.");
            }
            catch (Exception ex)
            {
                return ApiResponseDto<bool>.Fail($"Lỗi khi làm trống giỏ hàng: {ex.Message}");
            }
        }
    }
}
