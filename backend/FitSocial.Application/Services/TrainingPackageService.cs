using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.TrainingPackage;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FitSocial.Application.Services
{
    public class TrainingPackageService : ITrainingPackageService
    {
        private readonly ITrainingPackageRepository _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IReviewRepository _reviewRepository;
        private readonly IOrderRepository _orderRepository;

        public TrainingPackageService(ITrainingPackageRepository repository, IUnitOfWork unitOfWork, IReviewRepository reviewRepository, IOrderRepository orderRepository)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _reviewRepository = reviewRepository;
            _orderRepository = orderRepository;
        }

        public async Task<IEnumerable<TrainingPackageResponseDto>> GetMyPackagesAsync(Guid currentUserId)
        {
            var packages = await _repository.GetPackagesByCoachIdAsync(currentUserId);
            return packages.Where(p => p.IsActive == true).Select(MapToDto);
        }

        public async Task<IEnumerable<TrainingPackageResponseDto>> GetAllPackagesAsync(string? searchKeyword = null, decimal? maxPrice = null, Guid? coachId = null)
        {
            var packages = await _repository.GetAllActivePackagesAsync(searchKeyword, maxPrice, coachId);
            return packages.Select(MapToDto);
        }

        public async Task<TrainingPackageResponseDto?> GetPackageByIdAsync(Guid id)
        {
            var package = await _repository.GetByIdAsync(id);
            if (package == null)
            {
                return null;
            }
            return MapToDto(package);
        }

        public async Task<TrainingPackageResponseDto> CreatePackageAsync(Guid currentUserId, CreateTrainingPackageDto dto)
        {
            var package = new TrainingPackage
            {
                PackageId = Guid.NewGuid(),
                CoachId = currentUserId,
                Title = dto.Title,
                Description = dto.Description,
                Price = dto.Price,
                DurationDays = dto.DurationDays,
                SessionCount = dto.SessionCount,
                MinAge = dto.MinAge,
                TargetAudience = dto.TargetAudience,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            };

            if (dto.ImageUrls != null && dto.ImageUrls.Count > 0)
            {
                short sortOrder = 0;
                foreach (var url in dto.ImageUrls.Where(u => !string.IsNullOrWhiteSpace(u)))
                {
                    package.Media.Add(new TrainingPackageMedium
                    {
                        MediaId = Guid.Empty,
                        PackageId = package.PackageId,
                        MediaUrl = url.Trim(),
                        MediaType = "IMAGE",
                        SortOrder = sortOrder++,
                        CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
                    });
                }
            }

            await _repository.AddAsync(package);
            await _unitOfWork.SaveChangesAsync();

            var createdPackage = await _repository.GetByIdAsync(package.PackageId);
            return MapToDto(createdPackage ?? package);
        }

        public async Task<TrainingPackageResponseDto?> UpdatePackageAsync(Guid id, Guid currentUserId, UpdateTrainingPackageDto dto)
        {
            var package = await _repository.GetByIdAsync(id);
            if (package == null || package.CoachId != currentUserId)
            {
                return null;
            }

            if (dto.Title != null) package.Title = dto.Title;
            if (dto.Description != null) package.Description = dto.Description;
            if (dto.Price.HasValue) package.Price = dto.Price.Value;
            if (dto.DurationDays.HasValue) package.DurationDays = dto.DurationDays.Value;
            if (dto.SessionCount.HasValue) package.SessionCount = dto.SessionCount.Value;
            if (dto.MinAge.HasValue) package.MinAge = dto.MinAge.Value;
            if (dto.TargetAudience != null) package.TargetAudience = dto.TargetAudience;
            if (dto.IsActive.HasValue) package.IsActive = dto.IsActive.Value;

            if (dto.ImageUrls != null)
            {
                package.Media.Clear();
                short sortOrder = 0;
                foreach (var url in dto.ImageUrls.Where(u => !string.IsNullOrWhiteSpace(u)))
                {
                    package.Media.Add(new TrainingPackageMedium
                    {
                        MediaId = Guid.Empty,
                        PackageId = package.PackageId,
                        MediaUrl = url.Trim(),
                        MediaType = "IMAGE",
                        SortOrder = sortOrder++,
                        CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
                    });
                }
            }

            await _unitOfWork.SaveChangesAsync();

            return MapToDto(package);
        }

        public async Task<bool> SoftDeletePackageAsync(Guid id, Guid currentUserId)
        {
            var package = await _repository.GetByIdAsync(id);
            if (package == null || package.CoachId != currentUserId)
            {
                return false;
            }

            package.IsActive = false;
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<TrainingPackageResponseDto>> GetPurchasedPackagesAsync(Guid currentUserId)
        {
            var packages = await _repository.GetPurchasedPackagesAsync(currentUserId);
            return packages.Select(pkg =>
            {
                var dto = MapToDto(pkg);
                var userReview = pkg.Reviews?.FirstOrDefault(r => r.TraineeId == currentUserId);
                if (userReview != null)
                {
                    dto.HasReviewed = true;
                    dto.IsReviewEdited = userReview.IsEdited ?? false;
                    dto.UserRating = userReview.Rating;
                    dto.UserComment = userReview.Comment;
                }
                return dto;
            });
        }

        private static TrainingPackageResponseDto MapToDto(TrainingPackage entity)
        {
            return new TrainingPackageResponseDto
            {
                PackageId = entity.PackageId,
                CoachId = entity.CoachId,
                CoachName = entity.Coach?.Coach?.FullName ?? "Unknown Coach",
                Title = entity.Title,
                Description = entity.Description,
                Price = entity.Price,
                DurationDays = entity.DurationDays,
                SessionCount = entity.SessionCount,
                MinAge = entity.MinAge,
                TargetAudience = entity.TargetAudience,
                IsActive = entity.IsActive,
                CreatedAt = entity.CreatedAt,
                Media = entity.Media?.OrderBy(m => m.SortOrder).Select(m => new TrainingPackageMediaDto
                {
                    MediaId = m.MediaId,
                    PackageId = m.PackageId,
                    MediaUrl = m.MediaUrl,
                    MediaType = m.MediaType,
                    SortOrder = m.SortOrder,
                    CreatedAt = m.CreatedAt
                }).ToList() ?? new List<TrainingPackageMediaDto>()
            };
        }

        // ==============================================================
        // LOGIC REVIEWS & COACH REPLY THEO PACKAGE ID
        // ==============================================================
        public async Task<ApiResponseDto<PackageReviewSummaryDto>> GetPackageReviewsAsync(Guid packageId, CancellationToken cancellationToken = default)
        {
            var reviews = await _reviewRepository.GetReviewsByPackageIdAsync(packageId, cancellationToken);
            var summary = new PackageReviewSummaryDto();
            if (reviews != null && reviews.Any())
            {
                var validRatings = reviews.Where(r => r.Rating.HasValue).Select(r => r.Rating!.Value).ToList();
                summary.TotalReviews = validRatings.Count;
                summary.AverageRating = validRatings.Any() ? Math.Round(validRatings.Average(), 1) : 0.0;
                foreach (var rating in validRatings)
                {
                    if (summary.RatingCounts.ContainsKey(rating))
                    {
                        summary.RatingCounts[rating]++;
                    }
                }
                summary.Reviews = reviews.Select(r => new PackageReviewDto
                {
                    ReviewId = r.ReviewId,
                    PackageId = packageId,
                    TraineeId = r.TraineeId,
                    TraineeName = r.Trainee?.FullName ?? "FitSocial Member",
                    TraineeAvatarUrl = r.Trainee?.AvatarUrl,
                    Rating = r.Rating ?? 5,
                    Comment = r.Comment,
                    Reply = r.Reply,
                    CoachName = r.Coach?.Coach?.FullName,
                    CoachAvatarUrl = r.Coach?.Coach?.AvatarUrl,
                    CreatedAt = r.CreatedAt.HasValue ? DateTime.SpecifyKind(r.CreatedAt.Value, DateTimeKind.Utc) : null,
                    UpdatedAt = r.UpdatedAt.HasValue ? DateTime.SpecifyKind(r.UpdatedAt.Value, DateTimeKind.Utc) : null,
                    IsEdited = r.IsEdited ?? false,

                }).ToList();
            }
            return ApiResponseDto<PackageReviewSummaryDto>.Ok(summary);
        }
        public async Task<ApiResponseDto<bool>> SubmitPackageReviewAsync(Guid packageId, Guid traineeId, CreatePackageReviewDto dto, CancellationToken cancellationToken = default)
        {
            var package = await _repository.GetByIdAsync(packageId, cancellationToken);
            if (package == null) throw new NotFoundException("Training package not found.");
            // Kiểm tra xem Trainee đã mua gói này và thanh toán thành công chưa
            var orders = await _orderRepository.GetOrdersByBuyerIdAsync(traineeId, cancellationToken);
            var paidStatuses = new[] { Domain.Constants.PaymentConstants.OrderStatusPaid, "PAID", "ACTIVE", "COMPLETED" };
            bool hasPurchased = orders.Any(o =>
                o.OrderStatus != null &&
                paidStatuses.Contains(o.OrderStatus.ToUpper()) &&
                o.OrderDetails.Any(od => od.PackageId == packageId));
            if (!hasPurchased)
            {
                throw new ValidationException("You can only review a package that you have successfully purchased.");
            }
            // Nếu học viên đã từng đánh giá gói tập này thì Update, nếu chưa thì Thêm mới
            var existingReview = await _reviewRepository.GetReviewByPackageAndTraineeAsync(packageId, traineeId, cancellationToken);
            if (existingReview != null)
            {
                if (existingReview.IsEdited == true)
                {
                    throw new ValidationException("This review has been edited once and cannot be edited further.");
                }
                existingReview.Rating = dto.Rating;
                existingReview.Comment = dto.Comment;
                existingReview.UpdatedAt = DateTime.UtcNow;
                existingReview.IsEdited = true;
                _reviewRepository.Update(existingReview);
            }
            else
            {
                var review = new Review
                {
                    ReviewId = Guid.NewGuid(),
                    PackageId = packageId,
                    CoachId = package.CoachId,
                    TraineeId = traineeId,
                    Rating = dto.Rating,
                    Comment = dto.Comment,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = null,
                    IsEdited = false
                };
                await _reviewRepository.AddAsync(review);
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponseDto<bool>.Ok(true, "Review submitted successfully.");
        }
        public async Task<ApiResponseDto<bool>> ReplyToReviewAsync(Guid packageId, Guid reviewId, Guid coachId, ReplyReviewDto dto, CancellationToken cancellationToken = default)
        {
            var package = await _repository.GetByIdAsync(packageId, cancellationToken);
            if (package == null) throw new NotFoundException("Package not found.");
            // Chỉ Coach sở hữu gói tập mới được phản hồi review
            if (package.CoachId != coachId)
            {
                throw new UnauthorizedAccessException("Only the coach who owns this package can reply to its reviews.");
            }
            var review = await _reviewRepository.GetByIdAsync(reviewId, cancellationToken);
            if (review == null || review.PackageId != packageId)
            {
                throw new NotFoundException("Review not found for this package.");
            }
            review.Reply = dto.Reply?.Trim();
            review.UpdatedAt = DateTime.UtcNow;
            _reviewRepository.Update(review);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponseDto<bool>.Ok(true, "Reply posted successfully.");
        }
      
    }
}