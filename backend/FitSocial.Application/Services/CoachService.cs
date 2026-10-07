using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.DTOs.Coaches;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.TrainingPackage;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Application.Services;

public class CoachService : ICoachService
{
    private readonly ICoachProfileRepository _coachProfiles;
    private readonly ILocationRepository _locationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReviewRepository _reviewRepository;
    private readonly IOrderRepository _orderRepository;



    public CoachService(
        ICoachProfileRepository coachProfiles,
        ILocationRepository locationRepository,
        IUnitOfWork unitOfWork,
          IReviewRepository reviewRepository,
        IOrderRepository orderRepository)
    {
        _coachProfiles = coachProfiles;
        _locationRepository = locationRepository;
        _unitOfWork = unitOfWork;
        _reviewRepository = reviewRepository;
        _orderRepository = orderRepository;
    }

    public async Task<ApiResponseDto<List<TopCoachDto>>> GetTopCoachesAsync(int count = 3, CancellationToken cancellationToken = default)
    {
        if (count < 1)
        {
            throw new ValidationException("Count must be greater than or equal to 1.");
        }

        if (count > 50)
        {
            count = 50;
        }

        var coachSummaries = await _coachProfiles.GetTopCoachesAsync(count, cancellationToken);

        var dtoList = coachSummaries.Select(c => new TopCoachDto
        {
            CoachId = c.CoachId,
            FullName = c.FullName,
            AvatarUrl = c.AvatarUrl,
            Bio = c.Bio,
            ExperienceYears = c.ExperienceYears,
            TotalReviews = c.TotalReviews,
            Rating = Math.Round(c.AverageRating, 1)
        }).ToList();

        return ApiResponseDto<List<TopCoachDto>>.Ok(dtoList, "Top coaches retrieved successfully.");
    }

    public async Task<IEnumerable<CoachListDto>> GetAllCoachesAsync(string? searchKeyword = null, int? minExperience = null, string? sortBy = null)
    {
        var coaches = await _coachProfiles.GetAllCoachesWithDetailsAsync(searchKeyword, minExperience, sortBy);

        return coaches.Select(c => new CoachListDto
        {
            CoachId = c.CoachId,
            FullName = c.Coach?.FullName ?? "Unknown Coach",
            AvatarUrl = c.Coach?.AvatarUrl,
            ExperienceYears = c.ExperienceYears,
            Bio = c.Bio,
            CertificateUrl = c.CertificateUrl,
            Status = c.Status,
            ApprovalStatus = c.ApprovalStatus,
            Locations = c.Locations.Select(l => l.LocationName).ToList()
        });
    }

    public async Task<ApiResponseDto<CoachDetailDto>> GetCoachDetailsAsync(Guid coachId, CancellationToken cancellationToken = default)
    {
        var coach = await _coachProfiles.FindWithDetailsByCoachIdAsync(coachId, cancellationToken);
        if (coach == null)
        {
            throw new NotFoundException("Coach not found.");
        }

        var validReviews = coach.Reviews?.Where(r => r.Rating != null).ToList() ?? new();
        var avgRating = validReviews.Any() ? validReviews.Average(r => (double)r.Rating!.Value) : 0.0;

        var dto = new CoachDetailDto
        {
            CoachId = coach.CoachId,
            FullName = coach.Coach?.FullName ?? "Unknown Coach",
            Email = coach.Coach?.Email,
            PhoneNumber = coach.Coach?.PhoneNumber,
            AvatarUrl = coach.Coach?.AvatarUrl,
            Gender = coach.Coach?.Gender,
            DateOfBirth = coach.Coach?.DateOfBirth,

            Bio = coach.Bio,
            ExperienceYears = coach.ExperienceYears,
            ApprovalStatus = coach.ApprovalStatus,
            Status = coach.Status ?? "ACTIVE",
            UpdatedAt = coach.UpdatedAt,

            Rating = Math.Round(avgRating, 1),
            TotalReviews = validReviews.Count,

            Locations = coach.Locations.Select(l => new CoachLocationDetailDto
            {
                LocationId = l.LocationId,
                LocationName = l.LocationName,
                Address = l.Address
            }).ToList(),

            Certificates = coach.CoachCertificates.Select(cert => new CoachCertificateDetailDto
            {
                CertificateId = cert.CertificateId,
                CertificateName = cert.CertificateName,
                CertificateUrl = cert.CertificateUrl,
                IssuedBy = cert.IssuedBy,
                IssuedDate = cert.IssuedDate,
                ExpiryDate = cert.ExpiryDate,
                VerificationStatus = cert.VerificationStatus
            }).ToList(),

            Packages = coach.TrainingPackages.Where(p => p.IsActive == true).Select(p => new TrainingPackageResponseDto
            {
                PackageId = p.PackageId,
                CoachId = p.CoachId,
                CoachName = coach.Coach?.FullName ?? "Coach",
                Title = p.Title,
                Description = p.Description,
                Price = p.Price,
                DurationDays = p.DurationDays,
                SessionCount = p.SessionCount,
                MinAge = p.MinAge,
                TargetAudience = p.TargetAudience,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt
            }).ToList()
        };

        return ApiResponseDto<CoachDetailDto>.Ok(dto, "Coach details retrieved successfully.");
    }

    public async Task<ApiResponseDto<CoachDetailDto>> UpdateCoachProfileAsync(Guid coachId, UpdateCoachProfileDto dto, CancellationToken cancellationToken = default)
    {
        var coach = await _coachProfiles.FindWithDetailsByCoachIdAsync(coachId, cancellationToken);
        if (coach == null)
        {
            throw new NotFoundException("Coach profile not found.");
        }

        if (dto.Bio != null) coach.Bio = dto.Bio.Trim();
        if (dto.ExperienceYears.HasValue) coach.ExperienceYears = dto.ExperienceYears.Value;

        if (!string.IsNullOrWhiteSpace(dto.Status))
        {
            var status = dto.Status.Trim().ToUpperInvariant();
            if (status != "ACTIVE" && status != "INACTIVE" && status != "SUSPENDED")
            {
                throw new ValidationException("Invalid Status. Allowed values are 'ACTIVE', 'INACTIVE', or 'SUSPENDED'.");
            }
            coach.Status = status;
        }

        if (dto.LocationIds != null)
        {
            coach.Locations.Clear();
            if (dto.LocationIds.Any())
            {
                var allLocations = await _locationRepository.ListAllAsync(cancellationToken);
                var selectedLocations = allLocations.Where(l => dto.LocationIds.Contains(l.LocationId)).ToList();
                foreach (var loc in selectedLocations)
                {
                    coach.Locations.Add(loc);
                }
            }
        }

        coach.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetCoachDetailsAsync(coachId, cancellationToken);
    }

    public async Task<ApiResponseDto<bool>> SubmitReviewAsync(Guid coachId, Guid traineeId, CreateReviewDto dto, CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra xem Coach có tồn tại không
        var coach = await _coachProfiles.GetByIdAsync(coachId, cancellationToken);
        if (coach == null) throw new NotFoundException("Coach not found.");

        // 2. Lấy danh sách các đơn hàng của Trainee này
        var orders = await _orderRepository.GetOrdersByBuyerIdAsync(traineeId, cancellationToken);

        // 3. Kiểm tra xem có đơn hàng nào mua của Coach này và đã thanh toán (PAID/COMPLETED/ACTIVE) hay chưa
        var paidStatuses = new[] { FitSocial.Domain.Constants.PaymentConstants.OrderStatusPaid, "PAID", "ACTIVE", "COMPLETED" };
        bool hasPurchased = orders.Any(o => (o.CoachId == coachId || o.OrderDetails.Any(od => od.Package != null && od.Package.CoachId == coachId))
            && o.OrderStatus != null
            && paidStatuses.Contains(o.OrderStatus.ToUpper()));
        if (!hasPurchased)
        {
            throw new ValidationException("You can only review a coach if you have purchased their training package.");
        }

        // 4. Tạo Review
        var review = new FitSocial.Domain.Entities.Review
        {
            ReviewId = Guid.NewGuid(),
            CoachId = coachId,
            TraineeId = traineeId,
            Rating = dto.Rating,
            Comment = dto.Comment,
            CreatedAt = DateTime.UtcNow
        };

        await _reviewRepository.AddAsync(review);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponseDto<bool>.Ok(true, "Review submitted successfully.");
    }
    public async Task<ApiResponseDto<bool>> UpdateReviewAsync(Guid reviewId, Guid traineeId, FitSocial.Application.DTOs.Coaches.UpdateReviewDto dto, CancellationToken cancellationToken = default)
    {
        var review = await _reviewRepository.GetByIdAsync(reviewId, cancellationToken);
        if (review == null) throw new NotFoundException("Review not found.");

        // Chặn không cho phép sửa review của người khác
        if (review.TraineeId != traineeId) throw new UnauthorizedAccessException("You can only edit your own review.");

        review.Rating = dto.Rating;
        review.Comment = dto.Comment;
        review.UpdatedAt = DateTime.UtcNow;

        _reviewRepository.Update(review);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponseDto<bool>.Ok(true, "Review updated successfully.");
    }

    public async Task<ApiResponseDto<bool>> DeleteReviewAsync(Guid reviewId, Guid traineeId, CancellationToken cancellationToken = default)
    {
        var review = await _reviewRepository.GetByIdAsync(reviewId, cancellationToken);
        if (review == null) throw new NotFoundException("Review not found.");

        // Chặn không cho phép xóa review của người khác
        if (review.TraineeId != traineeId) throw new UnauthorizedAccessException("You can only delete your own review.");

        _reviewRepository.Remove(review);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponseDto<bool>.Ok(true, "Review deleted successfully.");
    }

}
