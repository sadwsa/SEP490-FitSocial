using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.TrainingPackage;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Domain.Policies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Application.Services
{
    public class TrainingPackageService : ITrainingPackageService
    {
        private readonly ITrainingPackageRepository _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IReviewRepository _reviewRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly IReviewMediaRepository _reviewMediaRepository;
        private readonly IUserRepository _userRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly IEmailService _emailService;

        public TrainingPackageService(ITrainingPackageRepository repository, IUnitOfWork unitOfWork, IReviewRepository reviewRepository, IOrderRepository orderRepository, IReviewMediaRepository reviewMediaRepository, IUserRepository userRepository, INotificationRepository notificationRepository, IEmailService emailService)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _reviewRepository = reviewRepository;
            _orderRepository = orderRepository;
            _reviewMediaRepository = reviewMediaRepository;
            _userRepository = userRepository;
            _notificationRepository = notificationRepository;
            _emailService = emailService;
        }

        // Backward-compatible overload for existing unit tests
        public TrainingPackageService(ITrainingPackageRepository repository, IUnitOfWork unitOfWork, IReviewRepository reviewRepository, IOrderRepository orderRepository)
            : this(repository, unitOfWork, reviewRepository, orderRepository, null!, null!, null!, null!)
        {
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
            var normalizedType = string.IsNullOrWhiteSpace(dto.PackageType) ? "BOTH" : dto.PackageType.Trim().ToUpperInvariant();
            var package = new TrainingPackage
            {
                PackageId = Guid.NewGuid(),
                CoachId = currentUserId,
                Title = dto.Title,
                Description = dto.Description,
                Price = dto.Price,
                DurationDays = dto.DurationDays,
                SessionCount = normalizedType == "MEAL" ? null : dto.SessionCount,
                MinAge = dto.MinAge,
                TargetAudience = dto.TargetAudience,
                PackageType = normalizedType,
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
            if (!string.IsNullOrWhiteSpace(dto.PackageType)) package.PackageType = dto.PackageType.Trim().ToUpperInvariant();
            package.SessionCount = package.PackageType == "MEAL" ? null : dto.SessionCount;
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

        public async Task<PagedResultDto<TrainingPackageResponseDto>> GetPurchasedPackagesAsync(
            Guid currentUserId,
            int pageNumber = 1,
            int pageSize = 10,
            string? searchKeyword = null,
            string? packageType = null,
            string? status = null,
            string? sortBy = null,
            CancellationToken cancellationToken = default)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            bool hasFilters = !string.IsNullOrWhiteSpace(searchKeyword)
                           || !string.IsNullOrWhiteSpace(packageType)
                           || !string.IsNullOrWhiteSpace(status)
                           || !string.IsNullOrWhiteSpace(sortBy);

            var (orderDetails, totalCount) = hasFilters
                ? await _repository.GetPurchasedOrderDetailsAsync(
                    currentUserId,
                    pageNumber,
                    pageSize,
                    searchKeyword,
                    packageType,
                    status,
                    sortBy,
                    cancellationToken)
                : await _repository.GetPurchasedOrderDetailsAsync(
                    currentUserId,
                    pageNumber,
                    pageSize,
                    cancellationToken);

            var nowUtc = DateTime.UtcNow;

            var dtos = (orderDetails ?? new List<OrderDetail>())
                .Where(od => od.Package != null)
                .Select(od => MapPurchasedOrderDetailToDto(od, currentUserId, nowUtc))
                .ToList();

            return PagedResultDto<TrainingPackageResponseDto>.Create(dtos, totalCount, pageNumber, pageSize);
        }

        private static TrainingPackageResponseDto MapPurchasedOrderDetailToDto(OrderDetail od, Guid currentUserId, DateTime nowUtc)
        {
            var pkg = od.Package!;
            var dto = MapToDto(pkg);

            // Ưu tiên dữ liệu Snapshot lúc mua từ OrderDetail
            dto.Title = !string.IsNullOrWhiteSpace(od.PackageTitle) ? od.PackageTitle : dto.Title;
            dto.Price = od.PackagePrice ?? dto.Price;
            dto.DurationDays = od.PackageDurationDays ?? dto.DurationDays;
            dto.CoachName = !string.IsNullOrWhiteSpace(od.CoachName) ? od.CoachName : dto.CoachName;
            dto.PackageType = od.PackageType ?? pkg.PackageType ?? "BOTH";

            dto.OrderDetailsId = od.OrderDetailsId;
            dto.OrderId = od.OrderId;
            dto.OrderCreatedAt = od.Order?.CreatedAt ?? od.CreatedAt;
            dto.OrderStatus = od.Order?.OrderStatus;

            var state = PurchasedPackageEnrollmentPolicy.Resolve(od, nowUtc);
            dto.RefundStatus = state.RefundStatus;
            dto.RefundStaffNote = state.RefundStaffNote;
            dto.TrainingPlanId = state.TrainingPlanId;
            dto.PlanStatus = state.PlanStatus;
            dto.MealPlanId = state.MealPlanId;
            dto.MealPlanStatus = state.MealPlanStatus;
            dto.ExpiresAt = state.ExpiresAt;
            dto.EnrollmentStatus = state.EnrollmentStatus;
            dto.RemainingDays = state.RemainingDays;

            // Review của học viên cho đơn hàng này (hỗ trợ cả fallback review cũ chưa gán OrderId)
            var userReview = pkg.Reviews?.FirstOrDefault(r => r.TraineeId == currentUserId && r.OrderId == od.OrderId)
                          ?? pkg.Reviews?.FirstOrDefault(r => r.TraineeId == currentUserId && r.OrderId == null);
            if (userReview != null)
            {
                dto.HasReviewed = true;
                dto.UserReviewId = userReview.ReviewId;
                dto.IsReviewEdited = userReview.IsEdited ?? false;
                dto.UserRating = userReview.Rating;
                dto.UserComment = userReview.Comment;
                dto.UserReviewImageUrls = userReview.ReviewMedia?
                    .OrderBy(m => m.SortOrder)
                    .Select(m => m.MediaUrl)
                    .ToList() ?? new List<string>();
            }
            else
            {
                dto.HasReviewed = false;
                dto.UserReviewId = null;
                dto.IsReviewEdited = false;
                dto.UserReviewImageUrls = new List<string>();
            }

            ApplyReviewEligibility(dto, state.IsRefundApproved, state.IsRefundPending);
            return dto;
        }

        private static bool IsPlanStatusActive(string? status) =>
            !string.IsNullOrWhiteSpace(status)
            && !string.Equals(status, "PENDING_COACH", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(status, "DRAFT", StringComparison.OrdinalIgnoreCase);

        private static void ApplyReviewEligibility(TrainingPackageResponseDto dto, bool isRefundApproved, bool isRefundPending)
        {
            if (isRefundApproved)
            {
                dto.CanReview = false;
                dto.CannotReviewReason = "This order has been refunded and cannot be reviewed.";
                return;
            }
            if (isRefundPending)
            {
                dto.CanReview = false;
                dto.CannotReviewReason = "Cannot submit or edit a review while a refund request is pending.";
                return;
            }
            if (dto.HasReviewed && dto.IsReviewEdited)
            {
                dto.CanReview = false;
                dto.CannotReviewReason = "Review edited once (Maximum 1 edit reached).";
                return;
            }
            if (dto.HasReviewed)
            {
                dto.CanReview = true;
                dto.CannotReviewReason = null;
                return;
            }

            bool workoutReady = dto.TrainingPlanId.HasValue && IsPlanStatusActive(dto.PlanStatus);
            bool mealReady = dto.MealPlanId.HasValue && IsPlanStatusActive(dto.MealPlanStatus);
            var normalizedPkgType = (dto.PackageType ?? "BOTH").Trim().ToUpperInvariant();

            if (normalizedPkgType == "WORKOUT" && !workoutReady)
            {
                dto.CanReview = false;
                dto.CannotReviewReason = "You can review this package once your workout plan is active.";
            }
            else if (normalizedPkgType == "MEAL" && !mealReady)
            {
                dto.CanReview = false;
                dto.CannotReviewReason = "You can review this package once your meal plan is active.";
            }
            else if (normalizedPkgType != "WORKOUT" && normalizedPkgType != "MEAL" && !workoutReady && !mealReady)
            {
                dto.CanReview = false;
                dto.CannotReviewReason = "You can review this package once your workout or meal plan is active.";
            }
            else
            {
                dto.CanReview = true;
                dto.CannotReviewReason = null;
            }
        }

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
                    OrderId = r.OrderId,
                    TraineeName = r.Trainee?.FullName ?? "FitConnect Member",
                    TraineeAvatarUrl = r.Trainee?.AvatarUrl,
                    Rating = r.Rating ?? 0,
                    Comment = r.Comment,
                    Reply = r.Reply,
                    CoachName = r.Coach?.Coach?.FullName,
                    CoachAvatarUrl = r.Coach?.Coach?.AvatarUrl,
                    CreatedAt = r.CreatedAt.HasValue ? DateTime.SpecifyKind(r.CreatedAt.Value, DateTimeKind.Utc) : null,
                    UpdatedAt = r.UpdatedAt.HasValue ? DateTime.SpecifyKind(r.UpdatedAt.Value, DateTimeKind.Utc) : null,
                    IsEdited = r.IsEdited ?? false,
                    Media = r.ReviewMedia?.Select(m => new ReviewMediaDto
                    {
                        ReviewMediaId = m.ReviewMediaId,
                        MediaUrl = m.MediaUrl,
                        MediaType = m.MediaType,
                        SortOrder = m.SortOrder
                    }).ToList() ?? new List<ReviewMediaDto>()

                }).ToList();
            }
            return ApiResponseDto<PackageReviewSummaryDto>.Ok(summary);
        }
        public async Task<ApiResponseDto<bool>> SubmitPackageReviewAsync(Guid packageId, Guid traineeId, CreatePackageReviewDto dto, CancellationToken cancellationToken = default)
        {
            if (dto == null)
                throw new ValidationException("Invalid review payload.");
            if (dto.Rating < 1 || dto.Rating > 5)
                throw new ValidationException("Rating must be between 1 and 5.");
            if (dto.Comment != null && dto.Comment.Length > 1000)
                throw new ValidationException("Comment cannot exceed 1000 characters.");

            var package = await _repository.GetByIdAsync(packageId, cancellationToken);
            if (package == null) throw new NotFoundException("Training package not found.");

            var coachProfile = package.Coach;
            if (coachProfile == null || coachProfile.ApprovalStatus?.Trim().ToUpperInvariant() != Domain.Constants.CoachConstants.ApprovalStatusApproved
                || (coachProfile.Status != null && coachProfile.Status.Trim().ToUpperInvariant() != Domain.Constants.CoachConstants.StatusActive))
            {
                throw new ValidationException("Coach of this package is not available for review.");
            }

         
            if (dto.OrderId == Guid.Empty)
                throw new ValidationException("OrderId is required to submit a review.");

            // Validate order: must belong to trainee, paid, and contain this package
            var order = await _orderRepository.GetOrderByIdWithDetailsAsync(dto.OrderId, cancellationToken);
            if (order == null || order.BuyerId != traineeId)
                throw new ValidationException("Order not found or does not belong to you.");
            var paidStatuses = new[] { Domain.Constants.PaymentConstants.OrderStatusPaid, Domain.Constants.PaymentConstants.OrderStatusActive, Domain.Constants.PaymentConstants.OrderStatusCompleted };
            if (order.OrderStatus == null || !paidStatuses.Contains(order.OrderStatus.ToUpper()))
                throw new ValidationException("You can only review a package from a paid order.");
            if (order.RefundRequests != null && order.RefundRequests.Any(r =>
                string.Equals(r.Status, "PENDING", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.Status, "APPROVED", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ValidationException("Cannot submit or edit a review while a refund request is pending or approved.");
            }
            var targetOrderDetail = order.OrderDetails.FirstOrDefault(od => od.PackageId == packageId);
            if (targetOrderDetail == null)
                throw new ValidationException("This order does not contain the specified package.");

            // Check plan progress eligibility if plans have been initialized on the order detail
            if ((targetOrderDetail.TrainingPlans != null && targetOrderDetail.TrainingPlans.Any()) ||
                (targetOrderDetail.MealPlans != null && targetOrderDetail.MealPlans.Any()))
            {
                var hasActiveWorkout = targetOrderDetail.TrainingPlans?.Any(tp =>
                    !string.IsNullOrWhiteSpace(tp.Status) &&
                    !string.Equals(tp.Status, "PENDING_COACH", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(tp.Status, "DRAFT", StringComparison.OrdinalIgnoreCase)) ?? false;
                var hasActiveMeal = targetOrderDetail.MealPlans?.Any(mp =>
                    !string.IsNullOrWhiteSpace(mp.Status) &&
                    !string.Equals(mp.Status, "PENDING_COACH", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(mp.Status, "DRAFT", StringComparison.OrdinalIgnoreCase)) ?? false;

                var normalizedPkgType = string.IsNullOrWhiteSpace(package.PackageType) ? "BOTH" : package.PackageType.Trim().ToUpperInvariant();
                var isPlanEligible = normalizedPkgType switch
                {
                    "WORKOUT" => hasActiveWorkout,
                    "MEAL" => hasActiveMeal,
                    _ => hasActiveWorkout || hasActiveMeal
                };

                if (!isPlanEligible)
                {
                    throw new ValidationException("You can only review a package after its training or meal plan has been activated.");
                }
            }

            // Validate images count (max 5 images) and URL safety (block javascript:/data: schemes)
            if (dto.ImageUrls != null)
            {
                if (dto.ImageUrls.Count > 5)
                    throw new ValidationException("Maximum 5 images allowed per review.");
                if (dto.ImageUrls.Any(u => string.IsNullOrWhiteSpace(u) || !IsSafeMediaUrl(u)))
                    throw new ValidationException("Invalid review image URL.");
            }

            var existingReview = await _reviewRepository.GetReviewByOrderIdAndTraineeAsync(dto.OrderId, traineeId, cancellationToken);
            if (existingReview != null)
            {
                throw new ValidationException("You have already reviewed this order. Please edit your existing review instead.");
            }

            var review = new Review
            {
                ReviewId = Guid.NewGuid(),
                PackageId = packageId,
                OrderId = dto.OrderId,
                CoachId = package.CoachId,
                TraineeId = traineeId,
                Rating = dto.Rating,
                Comment = dto.Comment,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null,
                IsEdited = false
            };
            await _reviewRepository.AddAsync(review);

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                throw new ValidationException("You have already reviewed this order. Please edit your existing review instead.");
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
            {
                throw new ValidationException("Review was modified by another request. Please reload and try again.");
            }

            // Save ReviewMedia immediately after Review persistence (before best-effort SMTP/Notification)
            if (dto.ImageUrls != null && dto.ImageUrls.Count > 0 && _reviewMediaRepository != null)
            {
                for (int i = 0; i < dto.ImageUrls.Count; i++)
                {
                    await _reviewMediaRepository.AddAsync(new ReviewMedia
                    {
                        ReviewMediaId = Guid.NewGuid(),
                        ReviewId = review.ReviewId,
                        MediaUrl = dto.ImageUrls[i].Trim(),
                        MediaType = "image",
                        SortOrder = i,
                        CreatedAt = DateTime.UtcNow
                    }, cancellationToken);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            // Send notification + email to coach (best-effort)
            try
            {
                var coachUser = await _userRepository.GetByIdAsync(package.CoachId, cancellationToken);
                var traineeUser = await _userRepository.GetByIdAsync(traineeId, cancellationToken);
                if (coachUser != null)
                {
                    var traineeName = traineeUser?.FullName ?? "A customer";
                    var packageTitle = package.Title ?? "this package";

                    var notification = new Notification
                    {
                        Id = Guid.NewGuid(),
                        UserId = package.CoachId,
                        ActorId = traineeId,
                        ReferenceId = dto.OrderId,
                        Type = "NEW_REVIEW",
                        Description = $"{traineeName} left a new {dto.Rating}-star review on your package \"{packageTitle}\".",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _notificationRepository.AddAsync(notification, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    if (!string.IsNullOrWhiteSpace(coachUser.Email))
                    {
                        var safeCoachName = System.Net.WebUtility.HtmlEncode(coachUser.FullName ?? "Coach");
                        var safeTraineeName = System.Net.WebUtility.HtmlEncode(traineeName);
                        var safePackageTitle = System.Net.WebUtility.HtmlEncode(packageTitle);
                        var safeComment = System.Net.WebUtility.HtmlEncode(dto.Comment ?? "");

                        await _emailService.SendEmailAsync(
                            coachUser.Email,
                            "[FitConnect] New review on your training package",
                            "<div style=\"font-family: Arial, sans-serif;\">" +
                            "<h2>New review received</h2>" +
                            "<p>Hi " + safeCoachName + ",</p>" +
                            "<p>" + safeTraineeName + " left a new " + dto.Rating + "-star review on your package <strong>" + safePackageTitle + "</strong>:</p>" +
                            "<blockquote style=\"border-left: 4px solid #FF5500; padding-left: 12px; color: #555;\">" + safeComment + "</blockquote>" +
                            "<p>Log in to FitConnect to view and reply.</p>" +
                            "</div>");
                    }
                }
            }
            catch
            {
                // Don't fail the whole review if notification/email fails
            }

            return ApiResponseDto<bool>.Ok(true, "Review submitted successfully.");
        }

        public async Task<ApiResponseDto<bool>> UpdatePackageReviewAsync(Guid packageId, Guid reviewId, Guid traineeId, UpdatePackageReviewDto dto, CancellationToken cancellationToken = default)
        {
            if (dto == null)
                throw new ValidationException("Invalid review payload.");
            if (dto.Rating < 1 || dto.Rating > 5)
                throw new ValidationException("Rating must be between 1 and 5.");
            if (dto.Comment != null && dto.Comment.Length > 1000)
                throw new ValidationException("Comment cannot exceed 1000 characters.");

            if (dto.ImageUrls != null)
            {
                if (dto.ImageUrls.Count > 5)
                    throw new ValidationException("Maximum 5 images allowed per review.");
                if (dto.ImageUrls.Any(u => string.IsNullOrWhiteSpace(u) || !IsSafeMediaUrl(u)))
                    throw new ValidationException("Invalid review image URL.");
            }

            var review = await _reviewRepository.GetByIdAsync(reviewId, cancellationToken);
            if (review == null || (review.PackageId.HasValue && review.PackageId.Value != packageId))
                throw new NotFoundException("Review not found.");

            if (review.TraineeId != traineeId)
                throw new UnauthorizedAccessException("You can only edit your own review.");

            if (review.IsEdited == true)
                throw new ValidationException("This review has been edited once and cannot be edited further.");

            if (review.OrderId.HasValue)
            {
                var order = await _orderRepository.GetOrderByIdWithDetailsAsync(review.OrderId.Value, cancellationToken);
                if (order?.RefundRequests != null && order.RefundRequests.Any(r =>
                    string.Equals(r.Status, "PENDING", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r.Status, "APPROVED", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ValidationException("Cannot submit or edit a review while a refund request is pending or approved.");
                }
            }

            review.Rating = dto.Rating;
            review.Comment = dto.Comment;
            review.UpdatedAt = DateTime.UtcNow;
            review.IsEdited = true;

            _reviewRepository.Update(review);
            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
            {
                throw new ValidationException("Review was modified by another request. Please reload and try again.");
            }

            if (dto.ImageUrls != null && _reviewMediaRepository != null)
            {
                var existingMedia = await _reviewMediaRepository.GetMediaByReviewIdAsync(review.ReviewId, cancellationToken);
                if (existingMedia != null)
                {
                    foreach (var media in existingMedia)
                    {
                        _reviewMediaRepository.Remove(media);
                    }
                }

                for (int i = 0; i < dto.ImageUrls.Count; i++)
                {
                    await _reviewMediaRepository.AddAsync(new ReviewMedia
                    {
                        ReviewMediaId = Guid.NewGuid(),
                        ReviewId = review.ReviewId,
                        MediaUrl = dto.ImageUrls[i].Trim(),
                        MediaType = "image",
                        SortOrder = i,
                        CreatedAt = DateTime.UtcNow
                    }, cancellationToken);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            try
            {
                var coachUser = await _userRepository.GetByIdAsync(review.CoachId, cancellationToken);
                var traineeUser = await _userRepository.GetByIdAsync(traineeId, cancellationToken);
                var package = await _repository.GetByIdAsync(packageId, cancellationToken);
                if (coachUser != null)
                {
                    var traineeName = traineeUser?.FullName ?? "A customer";
                    var packageTitle = package?.Title ?? "this package";

                    var notification = new Notification
                    {
                        Id = Guid.NewGuid(),
                        UserId = review.CoachId,
                        ActorId = traineeId,
                        ReferenceId = review.OrderId,
                        Type = "REVIEW_UPDATED",
                        Description = $"{traineeName} updated their {dto.Rating}-star review on your package \"{packageTitle}\".",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _notificationRepository.AddAsync(notification, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    if (!string.IsNullOrWhiteSpace(coachUser.Email))
                    {
                        var safeCoachName = System.Net.WebUtility.HtmlEncode(coachUser.FullName ?? "Coach");
                        var safeTraineeName = System.Net.WebUtility.HtmlEncode(traineeName);
                        var safePackageTitle = System.Net.WebUtility.HtmlEncode(packageTitle);
                        var safeComment = System.Net.WebUtility.HtmlEncode(dto.Comment ?? "");

                        await _emailService.SendEmailAsync(
                            coachUser.Email,
                            "[FitConnect] Review updated on your training package",
                            "<div style=\"font-family: Arial, sans-serif;\">" +
                            "<h2>Review updated</h2>" +
                            "<p>Hi " + safeCoachName + ",</p>" +
                            "<p>" + safeTraineeName + " updated their " + dto.Rating + "-star review on your package <strong>" + safePackageTitle + "</strong>:</p>" +
                            "<blockquote style=\"border-left: 4px solid #FF5500; padding-left: 12px; color: #555;\">" + safeComment + "</blockquote>" +
                            "<p>Log in to FitConnect to view and reply.</p>" +
                            "</div>");
                    }
                }
            }
            catch
            {
                // Don't fail the whole review update if notification/email fails
            }

            return ApiResponseDto<bool>.Ok(true, "Review updated successfully.");
        }
    private static TrainingPackageResponseDto MapToDto(TrainingPackage entity)
    {
        var coachCerts = entity.Coach?.CoachCertificates;
        bool coachHasExpired = CoachCertificatePolicy.HasExpiredCertificates(coachCerts);
        bool coachHasValid = CoachCertificatePolicy.HasValidCertificates(coachCerts);

        return new TrainingPackageResponseDto
        {
            PackageId = entity.PackageId,
            CoachId = entity.CoachId,
            CoachName = entity.Coach?.Coach?.FullName ?? "Unknown Coach",
            CoachHasExpiredCertificates = coachHasExpired,
            CoachHasValidCertificates = coachHasValid,
            Title = entity.Title,
            Description = entity.Description,
            Price = entity.Price,
            DurationDays = entity.DurationDays,
            SessionCount = entity.SessionCount,
            MinAge = entity.MinAge,
            TargetAudience = entity.TargetAudience,
            PackageType = entity.PackageType ?? "BOTH",
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

        private static bool IsSafeMediaUrl(string url)
        {
            var trimmed = url.Trim();
            if (!trimmed.Contains(':'))
                return true;
            return trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUniqueConstraintViolation(Microsoft.EntityFrameworkCore.DbUpdateException ex)
        {
            // PostgreSQL unique violation SQLSTATE 23505
            var msg = ex.InnerException?.Message ?? ex.Message;
            return msg.Contains("23505", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("unique", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
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

            // Validate reply content not empty and max length
            if (dto == null || string.IsNullOrWhiteSpace(dto.Reply))
            {
                throw new ValidationException("Reply content cannot be empty.");
            }
            var trimmedReply = dto.Reply.Trim();
            if (trimmedReply.Length > 1000)
            {
                throw new ValidationException("Reply cannot exceed 1000 characters.");
            }

            var review = await _reviewRepository.GetByIdAsync(reviewId, cancellationToken);
            if (review == null || review.PackageId != packageId)
            {
                throw new NotFoundException("Review not found for this package.");
            }

            // Chỉ cho phép reply 1 lần
            if (!string.IsNullOrWhiteSpace(review.Reply))
            {
                throw new ValidationException("This review already has a reply. You cannot reply again.");
            }

            review.Reply = trimmedReply;
            review.UpdatedAt = DateTime.UtcNow;
            _reviewRepository.Update(review);
            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
            {
                throw new ValidationException("Review was modified by another request. Please reload and try again.");
            }

            // Send notification + email to trainee
            try
            {
                var trainee = await _userRepository.GetByIdAsync(review.TraineeId, cancellationToken);
                if (trainee != null)
                {
                    var coachName = package.Coach?.Coach?.FullName ?? "Your coach";
                    var packageTitle = package.Title ?? "this package";

                    // Create in-app notification
                    var notification = new Notification
                    {
                        Id = Guid.NewGuid(),
                        UserId = review.TraineeId,
                        ActorId = coachId,
                        ReferenceId = review.ReviewId,
                        Type = "REVIEW_REPLY",
                        Description = $"{coachName} replied to your review on package \"{packageTitle}\".",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _notificationRepository.AddAsync(notification, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    // Send email notification
                    if (!string.IsNullOrWhiteSpace(trainee.Email))
                    {
                        var safeTraineeName = System.Net.WebUtility.HtmlEncode(trainee.FullName ?? "Customer");
                        var safePackageTitle = System.Net.WebUtility.HtmlEncode(packageTitle);
                        var safeReplyText = System.Net.WebUtility.HtmlEncode(review.Reply ?? "");

                        await _emailService.SendEmailAsync(
                            trainee.Email,
                            "[FitConnect] Coach replied to your review",
                            "<div style=\"font-family: Arial, sans-serif;\">" +
                            "<h2>New reply from Coach</h2>" +
                            "<p>Hi " + safeTraineeName + ",</p>" +
                            "<p>A coach has replied to your review on package <strong>" + safePackageTitle + "</strong>:</p>" +
                            "<blockquote style=\"border-left: 4px solid #FF5500; padding-left: 12px; color: #555;\">" + safeReplyText + "</blockquote>" +
                            "<p>Log in to FitConnect to view more details.</p>" +
                            "</div>");
                    }
                }
            }
            catch
            {
                // Don't fail the whole reply if notification/email fails
            }

            return ApiResponseDto<bool>.Ok(true, "Reply posted successfully.");
        }
      
    }
}