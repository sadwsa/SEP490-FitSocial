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
                    var reviews = coachProfile?.Reviews?.ToList();

                    var isOwnPackage = (pkg.CoachId == userId);
                    var isPackageActive = pkg.IsActive ?? true;
                    var isAvailable = isPackageActive && !isOwnPackage;
                    string? unavailableReason = null;

                    if (isOwnPackage)
                    {
                        unavailableReason = "Your own training package - Cannot purchase";
                    }
                    else if (!isPackageActive)
                    {
                        unavailableReason = "This package is currently suspended from taking new trainees";
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

                    var sportName = "Personal Coaching";
                    var thumbnailUrl = pkg.Media?.OrderBy(m => m.SortOrder).Select(m => m.MediaUrl).FirstOrDefault();

                    itemDtos.Add(new CartItemDto
                    {
                        CartId = c.CartId,
                        PackageId = pkg.PackageId,
                        Title = pkg.Title ?? "Custom Training Package",
                        Description = pkg.Description,
                        DurationDays = pkg.DurationDays,
                        DurationLabel = durationLabel,
                        Price = pkg.Price ?? 0,
                        Quantity = 1,
                        SessionCount = pkg.SessionCount,
                        MinAge = pkg.MinAge,
                        TargetAudience = pkg.TargetAudience,
                        ThumbnailUrl = thumbnailUrl,
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

                return ApiResponseDto<CartSummaryDto>.Ok(summary, "Cart retrieved successfully.");
            }
            catch (Exception ex)
            {
                return ApiResponseDto<CartSummaryDto>.Fail($"Error loading cart: {ex.Message}");
            }
        }

        public async Task<ApiResponseDto<int>> GetCartCountAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
            {
                return ApiResponseDto<int>.Ok(0);
            }

            try
            {
                var count = await _cartRepository.GetCartCountByUserIdAsync(userId, cancellationToken);
                return ApiResponseDto<int>.Ok(count);
            }
            catch (Exception ex)
            {
                return ApiResponseDto<int>.Fail($"Error counting cart items: {ex.Message}");
            }
        }

        public async Task<ApiResponseDto<CartItemDto>> AddToCartAsync(Guid userId, AddToCartDto dto, CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
            {
                return ApiResponseDto<CartItemDto>.Fail("User is not authenticated.");
            }

            if (dto == null)
            {
                return ApiResponseDto<CartItemDto>.Fail("Invalid training package information.");
            }

            var packageId = dto.GetEffectivePackageId();
            if (packageId == Guid.Empty)
            {
                return ApiResponseDto<CartItemDto>.Fail("Invalid training package information.");
            }

            if (dto.Quantity <= 0)
            {
                return ApiResponseDto<CartItemDto>.Fail("Quantity must be greater than 0.");
            }

            try
            {
                var package = await _packageRepository.GetByIdAsync(packageId, cancellationToken);
                if (package == null)
                {
                    return ApiResponseDto<CartItemDto>.Fail("Training package does not exist.");
                }

                if (package.CoachId == userId)
                {
                    return ApiResponseDto<CartItemDto>.Fail("You cannot add your own training package to cart.");
                }

                if (package.IsActive == false)
                {
                    return ApiResponseDto<CartItemDto>.Fail("This training package is currently suspended from taking trainees.");
                }

                var existing = await _cartRepository.GetCartItemAsync(userId, packageId, cancellationToken);
                if (existing != null)
                {
                    return ApiResponseDto<CartItemDto>.Fail("This training package is already in your cart. Each package can only be added once.");
                }

                var newCart = new Cart
                {
                    CartId = Guid.NewGuid(),
                    UserId = userId,
                    PackageId = packageId,
                    Quantity = 1, // Each personalized training package is added with quantity 1
                    CreatedAt = DateTime.UtcNow
                };

                await _cartRepository.AddAsync(newCart, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var resultDto = new CartItemDto
                {
                    CartId = newCart.CartId,
                    PackageId = newCart.PackageId,
                    Title = package.Title ?? "Training Package",
                    Description = package.Description,
                    DurationDays = package.DurationDays,
                    DurationLabel = (package.DurationDays.HasValue && package.DurationDays.Value >= 7 && package.DurationDays.Value % 7 == 0)
                        ? $"{package.DurationDays.Value / 7} Weeks ({package.DurationDays.Value} Days)"
                        : $"{package.DurationDays ?? 30} Days",
                    Price = package.Price ?? 0,
                    Quantity = 1,
                    SessionCount = package.SessionCount,
                    MinAge = package.MinAge,
                    TargetAudience = package.TargetAudience,
                    ThumbnailUrl = package.Media?.OrderBy(m => m.SortOrder).Select(m => m.MediaUrl).FirstOrDefault(),
                    CoachId = package.CoachId,
                    CoachName = package.Coach?.Coach?.FullName ?? "Coach",
                    CoachAvatarUrl = package.Coach?.Coach?.AvatarUrl,
                    IsActive = true,
                    IsAvailable = true,
                    CreatedAt = newCart.CreatedAt
                };

                return ApiResponseDto<CartItemDto>.Ok(resultDto, "Added to cart successfully.");
            }
            catch (Exception ex)
            {
                return ApiResponseDto<CartItemDto>.Fail($"Error adding to cart: {ex.Message}");
            }
        }

        public async Task<ApiResponseDto<bool>> RemoveCartItemAsync(Guid userId, Guid cartId, CancellationToken cancellationToken = default)
        {
            try
            {
                var cart = await _cartRepository.GetByIdAsync(cartId, cancellationToken);
                if (cart == null || cart.UserId != userId)
                {
                    return ApiResponseDto<bool>.Fail("Cart item does not exist or does not belong to you.");
                }

                _cartRepository.Remove(cart);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponseDto<bool>.Ok(true, "Removed package from cart successfully.");
            }
            catch (Exception ex)
            {
                return ApiResponseDto<bool>.Fail($"Error removing package from cart: {ex.Message}");
            }
        }

        public async Task<ApiResponseDto<bool>> ClearCartAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                await _cartRepository.ClearCartAsync(userId, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponseDto<bool>.Ok(true, "Cart cleared successfully.");
            }
            catch (Exception ex)
            {
                return ApiResponseDto<bool>.Fail($"Error clearing cart: {ex.Message}");
            }
        }
    }
}
