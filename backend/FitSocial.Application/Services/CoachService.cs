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
    private readonly INotificationRepository? _notificationRepository;

    public CoachService(
        ICoachProfileRepository coachProfiles,
        ILocationRepository locationRepository,
        IUnitOfWork unitOfWork,
        IReviewRepository reviewRepository,
        IOrderRepository orderRepository,
        INotificationRepository? notificationRepository = null)
    {
        _coachProfiles = coachProfiles;
        _locationRepository = locationRepository;
        _unitOfWork = unitOfWork;
        _reviewRepository = reviewRepository;
        _orderRepository = orderRepository;
        _notificationRepository = notificationRepository;
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

        // 3. Kiểm tra xem có đơn hàng nào mua của Coach này và đã thanh toán (PAID) hay chưa
        bool hasPurchased = orders.Any(o => o.CoachId == coachId && o.OrderStatus == FitSocial.Domain.Constants.PaymentConstants.OrderStatusPaid);
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

    public async Task<ApiResponseDto<CoachApplicationListResponseDto>> GetCoachApplicationsAsync(
        string? search = null,
        string? status = null,
        int? minExperience = null,
        int? maxExperience = null,
        string? sortBy = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (items, totalCount) = await _coachProfiles.GetPagedCoachApplicationsAsync(
                search, status, minExperience, maxExperience, sortBy, pageNumber, pageSize, cancellationToken);

            var countsDict = await _coachProfiles.GetCoachApplicationStatusCountsAsync(cancellationToken);

            var dtos = items.Select(c => new CoachApplicationItemDto
            {
                CoachId = c.CoachId,
                FullName = c.Coach?.FullName,
                Email = c.Coach?.Email,
                PhoneNumber = c.Coach?.PhoneNumber,
                AvatarUrl = c.Coach?.AvatarUrl,
                ExperienceYears = c.ExperienceYears,
                Bio = c.Bio,
                IdentityCardUrl = c.IdentityCardUrl,
                CertificateUrl = c.CertificateUrl,
                CertificatesCount = c.CoachCertificates?.Count ?? 0,
                ApprovalStatus = string.IsNullOrWhiteSpace(c.ApprovalStatus) ? "PENDING" : c.ApprovalStatus,
                Status = c.Status,
                CreatedAt = c.Coach?.CreatedAt ?? c.UpdatedAt,
                UpdatedAt = c.UpdatedAt,
                ApprovedByName = c.ApprovedByNavigation?.FullName,
                Locations = c.Locations?.Select(l => l.LocationName).ToList() ?? new List<string>()
            }).ToList();

            var pagedResult = PagedResultDto<CoachApplicationItemDto>.Create(dtos, totalCount, pageNumber, pageSize);
            var statusCounts = new CoachApplicationStatusCountsDto
            {
                All = countsDict.GetValueOrDefault("all", 0),
                Pending = countsDict.GetValueOrDefault("pending", 0),
                Approved = countsDict.GetValueOrDefault("approved", 0),
                Rejected = countsDict.GetValueOrDefault("rejected", 0)
            };

            var response = new CoachApplicationListResponseDto
            {
                Applications = pagedResult,
                StatusCounts = statusCounts
            };

            return ApiResponseDto<CoachApplicationListResponseDto>.Ok(response, "Coach application requests retrieved successfully.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<CoachApplicationListResponseDto>.Fail($"Error retrieving coach applications: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<CoachApplicationDetailDto>> GetCoachApplicationDetailsAsync(
        Guid coachId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var c = await _coachProfiles.GetCoachApplicationDetailsAsync(coachId, cancellationToken);
            if (c == null)
            {
                return ApiResponseDto<CoachApplicationDetailDto>.Fail("Coach application not found.");
            }

            var detail = MapToCoachApplicationDetailDto(c);
            return ApiResponseDto<CoachApplicationDetailDto>.Ok(detail, "Coach application details retrieved successfully.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<CoachApplicationDetailDto>.Fail($"Error retrieving coach application details: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<CoachApplicationStatusCountsDto>> GetCoachApplicationStatusCountsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var countsDict = await _coachProfiles.GetCoachApplicationStatusCountsAsync(cancellationToken);
            var statusCounts = new CoachApplicationStatusCountsDto
            {
                All = countsDict.GetValueOrDefault("all", 0),
                Pending = countsDict.GetValueOrDefault("pending", 0),
                Approved = countsDict.GetValueOrDefault("approved", 0),
                Rejected = countsDict.GetValueOrDefault("rejected", 0)
            };
            return ApiResponseDto<CoachApplicationStatusCountsDto>.Ok(statusCounts);
        }
        catch (Exception ex)
        {
            return ApiResponseDto<CoachApplicationStatusCountsDto>.Fail($"Error retrieving status counts: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<CoachApplicationDetailDto>> ApproveCoachApplicationAsync(
        Guid coachId,
        Guid approverId,
        ApproveCoachApplicationRequestDto? request = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var profile = await _coachProfiles.GetCoachApplicationDetailsAsync(coachId, cancellationToken);
            if (profile == null)
            {
                return ApiResponseDto<CoachApplicationDetailDto>.Fail("Coach application not found.");
            }

            var currentStatus = profile.ApprovalStatus?.Trim().ToUpperInvariant() ?? "PENDING";
            if (currentStatus != "PENDING")
            {
                return ApiResponseDto<CoachApplicationDetailDto>.Fail($"Only pending applications can be approved. Current status is {currentStatus}.");
            }

            var now = DateTime.UtcNow;

            // 1. Update CoachProfile
            profile.ApprovalStatus = "APPROVED";
            profile.ApprovedBy = approverId;
            profile.Status = "ACTIVE";
            profile.UpdatedAt = now;

            // 2. Update Coach User: unlock applicant and ensure coach role
            if (profile.Coach != null)
            {
                profile.Coach.RoleCode = Domain.Constants.RoleConstants.Coach;
                profile.Coach.IsLocked = false;
                profile.Coach.LockedBy = null;
                profile.Coach.UpdatedAt = now;
            }

            // 3. Update pending certificates
            if (profile.CoachCertificates != null)
            {
                foreach (var cert in profile.CoachCertificates)
                {
                    if (string.IsNullOrWhiteSpace(cert.VerificationStatus) ||
                        string.Equals(cert.VerificationStatus, "PENDING", StringComparison.OrdinalIgnoreCase))
                    {
                        cert.VerificationStatus = "APPROVED";
                        cert.VerifiedBy = approverId;
                        cert.VerifiedAt = now;
                        cert.UpdatedAt = now;
                    }
                }
            }

            // 4. Update pending eKYC
            if (profile.CoachEkycVerifications != null)
            {
                foreach (var ekyc in profile.CoachEkycVerifications)
                {
                    if (string.IsNullOrWhiteSpace(ekyc.VerificationStatus) ||
                        string.Equals(ekyc.VerificationStatus, "PENDING", StringComparison.OrdinalIgnoreCase))
                    {
                        ekyc.VerificationStatus = "APPROVED";
                        ekyc.UpdatedAt = now;
                    }
                }
            }

            // 5. In-app notification
            if (_notificationRepository != null)
            {
                var notification = new Domain.Entities.Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = profile.CoachId,
                    ActorId = approverId,
                    Type = "COACH_APPLICATION_APPROVED",
                    Description = string.IsNullOrWhiteSpace(request?.Note)
                        ? "Congratulations! Your coach application has been approved. You can now access your coach dashboard."
                        : $"Congratulations! Your coach application has been approved. Note: {request.Note}",
                    CreatedAt = now,
                    IsRead = false
                };
                await _notificationRepository.AddAsync(notification, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var detail = MapToCoachApplicationDetailDto(profile);
            return ApiResponseDto<CoachApplicationDetailDto>.Ok(detail, "Coach application approved successfully.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<CoachApplicationDetailDto>.Fail($"Error approving coach application: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<CoachApplicationDetailDto>> RejectCoachApplicationAsync(
        Guid coachId,
        Guid rejectorId,
        RejectCoachApplicationRequestDto? request = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var profile = await _coachProfiles.GetCoachApplicationDetailsAsync(coachId, cancellationToken);
            if (profile == null)
            {
                return ApiResponseDto<CoachApplicationDetailDto>.Fail("Coach application not found.");
            }

            var currentStatus = profile.ApprovalStatus?.Trim().ToUpperInvariant() ?? "PENDING";
            if (currentStatus != "PENDING")
            {
                return ApiResponseDto<CoachApplicationDetailDto>.Fail($"Only pending applications can be rejected. Current status is {currentStatus}.");
            }

            var now = DateTime.UtcNow;

            // 1. Update CoachProfile
            profile.ApprovalStatus = "REJECTED";
            profile.ApprovedBy = rejectorId;
            profile.UpdatedAt = now;

            // 2. Applicant User remains locked but not deleted
            if (profile.Coach != null)
            {
                profile.Coach.IsLocked = true;
                profile.Coach.UpdatedAt = now;
            }

            // 3. Update pending certificates
            if (profile.CoachCertificates != null)
            {
                foreach (var cert in profile.CoachCertificates)
                {
                    if (string.IsNullOrWhiteSpace(cert.VerificationStatus) ||
                        string.Equals(cert.VerificationStatus, "PENDING", StringComparison.OrdinalIgnoreCase))
                    {
                        cert.VerificationStatus = "REJECTED";
                        cert.VerifiedBy = rejectorId;
                        cert.VerifiedAt = now;
                        cert.RejectedReason = request?.Reason;
                        cert.UpdatedAt = now;
                    }
                }
            }

            // 4. Update pending eKYC
            if (profile.CoachEkycVerifications != null)
            {
                foreach (var ekyc in profile.CoachEkycVerifications)
                {
                    if (string.IsNullOrWhiteSpace(ekyc.VerificationStatus) ||
                        string.Equals(ekyc.VerificationStatus, "PENDING", StringComparison.OrdinalIgnoreCase))
                    {
                        ekyc.VerificationStatus = "REJECTED";
                        ekyc.FailureReason = request?.Reason;
                        ekyc.UpdatedAt = now;
                    }
                }
            }

            // 5. In-app notification
            if (_notificationRepository != null)
            {
                var notification = new Domain.Entities.Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = profile.CoachId,
                    ActorId = rejectorId,
                    Type = "COACH_APPLICATION_REJECTED",
                    Description = string.IsNullOrWhiteSpace(request?.Reason)
                        ? "Your coach application has been rejected. Please check your email for details and resubmission instructions."
                        : $"Your coach application has been rejected. Reason: {request.Reason}",
                    CreatedAt = now,
                    IsRead = false
                };
                await _notificationRepository.AddAsync(notification, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var detail = MapToCoachApplicationDetailDto(profile);
            return ApiResponseDto<CoachApplicationDetailDto>.Ok(detail, "Coach application rejected successfully.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<CoachApplicationDetailDto>.Fail($"Error rejecting coach application: {ex.Message}");
        }
    }

    private static CoachApplicationDetailDto MapToCoachApplicationDetailDto(Domain.Entities.CoachProfile c)
    {
        var latestEkyc = c.CoachEkycVerifications?.OrderByDescending(e => e.CreatedAt).FirstOrDefault();

        return new CoachApplicationDetailDto
        {
            CoachId = c.CoachId,
            FullName = c.Coach?.FullName,
            Email = c.Coach?.Email,
            PhoneNumber = c.Coach?.PhoneNumber,
            AvatarUrl = c.Coach?.AvatarUrl,
            Gender = c.Coach?.Gender,
            DateOfBirth = c.Coach?.DateOfBirth,
            ExperienceYears = c.ExperienceYears,
            Bio = c.Bio,
            IdentityCardUrl = c.IdentityCardUrl,
            CertificateUrl = c.CertificateUrl,
            CertificatesCount = c.CoachCertificates?.Count ?? 0,
            ApprovalStatus = string.IsNullOrWhiteSpace(c.ApprovalStatus) ? "PENDING" : c.ApprovalStatus,
            Status = c.Status,
            CreatedAt = c.Coach?.CreatedAt ?? c.UpdatedAt,
            UpdatedAt = c.UpdatedAt,
            ApprovedByName = c.ApprovedByNavigation?.FullName,
            Locations = c.Locations?.Select(l => l.LocationName).ToList() ?? new List<string>(),
            Certificates = c.CoachCertificates?.Select(cert => new CoachCertificateDetailDto
            {
                CertificateId = cert.CertificateId,
                CertificateName = cert.CertificateName,
                CertificateUrl = cert.CertificateUrl,
                IssuedBy = cert.IssuedBy,
                IssuedDate = cert.IssuedDate,
                ExpiryDate = cert.ExpiryDate,
                VerificationStatus = cert.VerificationStatus
            }).ToList() ?? new List<CoachCertificateDetailDto>(),
            Ekyc = latestEkyc == null ? null : new CoachApplicationEkycDto
            {
                FullNameOnCard = latestEkyc.FullNameOnCard,
                DateOfBirthOnCard = latestEkyc.DateOfBirthOnCard,
                Sex = latestEkyc.Sex,
                FrontCardUrl = latestEkyc.FrontCardUrl,
                BackCardUrl = latestEkyc.BackCardUrl,
                FaceImageUrl = latestEkyc.FaceImageUrl,
                LivenessScore = latestEkyc.LivenessScore,
                FaceMatchConfidence = latestEkyc.FaceMatchConfidence,
                VerificationStatus = latestEkyc.VerificationStatus,
                FailureReason = latestEkyc.FailureReason,
                CreatedAt = latestEkyc.CreatedAt
            }
        };
    }
}
