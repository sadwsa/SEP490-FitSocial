using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Cart;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Application.Services;

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

    public async Task<CartItemDto> AddToCartAsync(Guid userId, AddToCartRequestDto dto, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ValidationException("User is not authenticated.");
        }

        if (dto == null)
        {
            throw new ValidationException("Request payload is required.");
        }

        var packageId = dto.GetEffectivePackageId();
        if (packageId == Guid.Empty)
        {
            throw new ValidationException("Product ID is required.");
        }

        if (dto.Quantity <= 0)
        {
            throw new ValidationException("Quantity must be greater than 0.");
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("User account not found.");
        }

        var package = await _packageRepository.GetByIdAsync(packageId, cancellationToken);
        if (package == null)
        {
            throw new NotFoundException("Training package not found.");
        }

        if (package.IsActive != true)
        {
            throw new BusinessException("Training package is no longer available.");
        }

        if (package.CoachId == userId)
        {
            throw new BusinessException("You cannot add your own package to the cart.");
        }

        var existingItem = await _cartRepository.GetCartItemAsync(userId, packageId, cancellationToken);
        if (existingItem != null)
        {
            existingItem.Quantity += dto.Quantity;
        }
        else
        {
            existingItem = new Cart
            {
                CartId = Guid.NewGuid(),
                UserId = userId,
                PackageId = packageId,
                Quantity = dto.Quantity,
                CreatedAt = DateTime.UtcNow
            };
            await _cartRepository.AddAsync(existingItem, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var coachName = package.Coach?.Coach?.FullName;
        if (string.IsNullOrWhiteSpace(coachName))
        {
            var coachUser = await _userRepository.GetByIdAsync(package.CoachId, cancellationToken);
            coachName = coachUser?.FullName ?? coachUser?.Email ?? "Coach";
        }

        return new CartItemDto
        {
            CartId = existingItem.CartId,
            UserId = existingItem.UserId,
            PackageId = existingItem.PackageId,
            PackageTitle = package.Title,
            Price = package.Price,
            CoachName = coachName,
            DurationDays = package.DurationDays,
            Quantity = existingItem.Quantity,
            CreatedAt = existingItem.CreatedAt
        };
    }

    public async Task<int> GetCartCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return 0;
        }

        return await _cartRepository.GetCartCountByUserIdAsync(userId, cancellationToken);
    }
}
