using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Reports;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Application.Services;

public class ReportService : IReportService
{
    private readonly IReportRepository _reportRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderRepository? _orderRepository;
    private readonly ICoachProfileRepository? _coachProfileRepository;
    private readonly INotificationRepository? _notificationRepository;
    private readonly IPostRepository? _postRepository;
    private readonly IPostRealtimeNotifier? _postRealtimeNotifier;

    public ReportService(
        IReportRepository reportRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IOrderRepository orderRepository,
        ICoachProfileRepository coachProfileRepository,
        INotificationRepository notificationRepository,
        IPostRepository postRepository,
        IPostRealtimeNotifier? postRealtimeNotifier = null)
    {
        _reportRepository = reportRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _orderRepository = orderRepository;
        _coachProfileRepository = coachProfileRepository;
        _notificationRepository = notificationRepository;
        _postRepository = postRepository;
        _postRealtimeNotifier = postRealtimeNotifier;
    }

    public ReportService(
        IReportRepository reportRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IOrderRepository orderRepository,
        ICoachProfileRepository coachProfileRepository,
        INotificationRepository notificationRepository)
        : this(reportRepository, userRepository, unitOfWork, orderRepository, coachProfileRepository, notificationRepository, null!, null)
    {
    }

    public ReportService(
        IReportRepository reportRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
        : this(reportRepository, userRepository, unitOfWork, null!, null!, null!, null!, null)
    {
    }


    public async Task<ApiResponseDto<PagedResultDto<ReportListItemDto>>> GetReportsAsync(
        GetReportsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            query ??= new GetReportsQueryDto();

            var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
            var pageSize = query.PageSize < 1 ? 10 : (query.PageSize > 100 ? 100 : query.PageSize);

            var normalizedTargetType = query.TargetType?.Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(normalizedTargetType) &&
                normalizedTargetType != "POST" &&
                normalizedTargetType != "USER")
            {
                return ApiResponseDto<PagedResultDto<ReportListItemDto>>.Fail("Invalid target type. Allowed values are 'POST' or 'USER'.");
            }

            var (items, totalCount) = await _reportRepository.GetPagedReportsAsync(
                status: query.Status?.Trim(),
                targetType: normalizedTargetType,
                reporterId: query.ReporterId,
                fromDate: query.FromDate,
                toDate: query.ToDate,
                searchTerm: query.SearchTerm?.Trim(),
                pageNumber: pageNumber,
                pageSize: pageSize,
                type: query.Type?.Trim(),
                appealStatus: query.AppealStatus?.Trim(),
                cancellationToken: cancellationToken);

            var coachIds = items
                .Select(r => r.ReportedUser ?? r.ReportedPost?.Author)
                .Where(u => u != null && string.Equals(u.RoleCode, RoleConstants.Coach, StringComparison.OrdinalIgnoreCase))
                .Select(u => u!.UserId)
                .Distinct()
                .ToList();

            var coachSoldDict = new Dictionary<Guid, bool>();
            if (_orderRepository != null)
            {
                foreach (var cId in coachIds)
                {
                    coachSoldDict[cId] = await _orderRepository.HasSoldTrainingPackageAsync(cId, cancellationToken);
                }
            }

            var dtos = items.Select(r =>
            {
                var targetUser = r.ReportedUser ?? r.ReportedPost?.Author;
                bool? sold = (targetUser != null && coachSoldDict.TryGetValue(targetUser.UserId, out var s)) ? s : null;
                return MapToReportDto(r, sold);
            }).ToList();

            var pagedResult = PagedResultDto<ReportListItemDto>.Create(dtos, totalCount, pageNumber, pageSize);
            return ApiResponseDto<PagedResultDto<ReportListItemDto>>.Ok(pagedResult, "Reports retrieved successfully.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<PagedResultDto<ReportListItemDto>>.Fail($"Error retrieving reports: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<ReportListItemDto>> GetReportByIdAsync(
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        var report = await _reportRepository.GetReportWithDetailsByIdAsync(reportId, cancellationToken);
        if (report == null)
        {
            return ApiResponseDto<ReportListItemDto>.Fail("Report not found.");
        }

        var targetUser = report.ReportedUser ?? report.ReportedPost?.Author;
        bool? hasSold = null;
        if (targetUser != null && _orderRepository != null &&
            string.Equals(targetUser.RoleCode, RoleConstants.Coach, StringComparison.OrdinalIgnoreCase))
        {
            hasSold = await _orderRepository.HasSoldTrainingPackageAsync(targetUser.UserId, cancellationToken);
        }

        return ApiResponseDto<ReportListItemDto>.Ok(MapToReportDto(report, hasSold));
    }


    public async Task<ApiResponseDto<ReportListItemDto>> ResolveReportAsync(
        Guid reportId,
        Guid staffId,
        ResolveReportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Status))
        {
            throw new ValidationException("Status is required.");
        }

        var report = await _reportRepository.GetReportWithDetailsByIdAsync(reportId, cancellationToken);
        if (report == null)
        {
            throw new NotFoundException("Report not found.");
        }

        var now = DateTime.UtcNow;
        report.Status = request.Status.Trim().ToUpperInvariant();
        report.ResolvedBy = staffId;
        report.ResolvedAt = now;
        report.NotifiedReportedUserAt = now;

        // If report is resolved and marked to issue warning
        if (request.IssueWarning && report.ReportedUserId.HasValue)
        {
            var reportedUser = await _userRepository.GetByIdAsync(report.ReportedUserId.Value, cancellationToken);
            if (reportedUser != null)
            {
                reportedUser.WarningCount += 1;
                // If warning threshold (3) is reached, auto-lock account and bump token version
                if (reportedUser.WarningCount >= 3)
                {
                    reportedUser.IsLocked = true;
                    reportedUser.LockedBy = staffId;
                    reportedUser.TokenVersion += 1;
                }
                reportedUser.UpdatedAt = now;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponseDto<ReportListItemDto>.Ok(MapToReportDto(report), "Report resolved successfully.");
    }

    public async Task<ApiResponseDto<ReportListItemDto>> SubmitAppealAsync(
        Guid reportId,
        Guid userId,
        SubmitAppealRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.AppealContent))
        {
            throw new ValidationException("Appeal content is required.");
        }

        var report = await _reportRepository.GetReportWithDetailsByIdAsync(reportId, cancellationToken);
        if (report == null)
        {
            throw new NotFoundException("Report not found.");
        }

        // Only the reported user (or author of reported post) can appeal
        var isReportedUser = (report.ReportedUserId.HasValue && report.ReportedUserId.Value == userId)
            || (report.ReportedPost != null && report.ReportedPost.AuthorId == userId);

        if (!isReportedUser)
        {
            throw new ForbiddenException("Only the reported user can submit an appeal for this report.");
        }

        if (report.AppealStatus == "APPROVED" || report.AppealStatus == "PENDING")
        {
            throw new BusinessException($"Cannot submit appeal because current appeal status is '{report.AppealStatus}'.");
        }

        var now = DateTime.UtcNow;
        report.AppealStatus = "PENDING";
        report.AppealContent = request.AppealContent.Trim();
        report.AppealedAt = now;

        if (request.MediaUrls != null && request.MediaUrls.Any())
        {
            short sortOrder = (short)report.ReportMedia.Count;
            foreach (var url in request.MediaUrls.Where(u => !string.IsNullOrWhiteSpace(u)))
            {
                report.ReportMedia.Add(new ReportMedium
                {
                    MediaId = Guid.NewGuid(),
                    ReportId = report.ReportId,
                    MediaUrl = url.Trim(),
                    MediaType = "IMAGE",
                    MediaFor = "APPEAL",
                    SortOrder = sortOrder++,
                    CreatedAt = now
                });
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponseDto<ReportListItemDto>.Ok(MapToReportDto(report), "Appeal submitted successfully.");
    }

    public async Task<ApiResponseDto<ReportListItemDto>> ReviewAppealAsync(
        Guid reportId,
        Guid staffId,
        ReviewAppealRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.AppealStatus))
        {
            throw new ValidationException("AppealStatus is required.");
        }

        var report = await _reportRepository.GetReportWithDetailsByIdAsync(reportId, cancellationToken);
        if (report == null)
        {
            throw new NotFoundException("Report not found.");
        }

        var normalizedStatus = request.AppealStatus.Trim().ToUpperInvariant();
        if (normalizedStatus != "APPROVED" && normalizedStatus != "REJECTED")
        {
            throw new ValidationException("Invalid AppealStatus. Allowed values are 'APPROVED' or 'REJECTED'.");
        }

        var now = DateTime.UtcNow;
        report.AppealStatus = normalizedStatus;
        report.AppealReviewedBy = staffId;
        report.AppealReviewedAt = now;
        report.AppealReviewNote = request.AppealReviewNote?.Trim();

        // If appeal was approved, reverse warning if applicable
        if (normalizedStatus == "APPROVED" && report.ReportedUserId.HasValue)
        {
            var reportedUser = await _userRepository.GetByIdAsync(report.ReportedUserId.Value, cancellationToken);
            if (reportedUser != null && reportedUser.WarningCount > 0)
            {
                reportedUser.WarningCount -= 1;
                if (reportedUser.IsLocked == true && reportedUser.WarningCount < 3)
                {
                    reportedUser.IsLocked = false;
                    reportedUser.LockedBy = null;
                }
                reportedUser.UpdatedAt = now;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponseDto<ReportListItemDto>.Ok(MapToReportDto(report), "Appeal reviewed successfully.");
    }

    public async Task<ApiResponseDto<ReportListItemDto>> ReportUserAsync(
        Guid reporterId,
        CreateUserReportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (reporterId == Guid.Empty)
        {
            throw new ValidationException("User is not authenticated.");
        }

        if (request == null || request.ReportedUserId == Guid.Empty)
        {
            throw new ValidationException("Reported user ID is required.");
        }

        if (request.ReportedUserId == reporterId)
        {
            throw new BusinessException("You cannot report yourself.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ValidationException("Reason is required.");
        }

        var reportedUser = await _userRepository.GetByIdAsync(request.ReportedUserId, cancellationToken);
        if (reportedUser == null)
        {
            throw new NotFoundException("Reported user not found.");
        }

        var now = DateTime.UtcNow;
        var report = new Report
        {
            ReportId = Guid.NewGuid(),
            ReporterId = reporterId,
            ReportedUserId = request.ReportedUserId,
            Reason = request.Reason.Trim(),
            Description = request.Description?.Trim(),
            Type = string.IsNullOrWhiteSpace(request.Type) ? "USER" : request.Type.Trim().ToUpperInvariant(),
            Status = "PENDING",
            CreatedAt = now
        };

        if (request.MediaUrls != null && request.MediaUrls.Any())
        {
            short sortOrder = 0;
            foreach (var url in request.MediaUrls.Where(u => !string.IsNullOrWhiteSpace(u)))
            {
                report.ReportMedia.Add(new ReportMedium
                {
                    MediaId = Guid.NewGuid(),
                    ReportId = report.ReportId,
                    MediaUrl = url.Trim(),
                    MediaType = "IMAGE",
                    MediaFor = "REPORT",
                    SortOrder = sortOrder++,
                    CreatedAt = now
                });
            }
        }

        await _reportRepository.AddAsync(report, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var detailedReport = await _reportRepository.GetReportWithDetailsByIdAsync(report.ReportId, cancellationToken);
        return ApiResponseDto<ReportListItemDto>.Ok(MapToReportDto(detailedReport ?? report, null), "User reported successfully.");
    }

    public async Task<ApiResponseDto<ReportListItemDto>> ProcessViolationReportAsync(
        Guid reportId,
        Guid staffId,
        ProcessViolationReportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Action))
        {
            throw new ValidationException("Action is required.");
        }

        var report = await _reportRepository.GetReportWithDetailsByIdAsync(reportId, cancellationToken);
        if (report == null)
        {
            throw new NotFoundException("Report not found.");
        }

        var currentStatus = report.Status?.Trim().ToUpperInvariant();
        if (currentStatus == "RESOLVED" || currentStatus == "REJECTED" || currentStatus == "DISMISSED")
        {
            throw new BusinessException($"Report has already been processed with status '{report.Status}'.");
        }

        var action = request.Action.Trim().ToUpperInvariant();
        var validActions = new[] { "WARN", "BLOCK", "LOCK", "DELETE_POST", "REJECT", "DISMISS" };
        if (!validActions.Contains(action))
        {
            throw new ValidationException($"Invalid action '{request.Action}'. Allowed actions are WARN, BLOCK, LOCK, DELETE_POST, REJECT, DISMISS.");
        }

        var now = DateTime.UtcNow;

        // If action is REJECT or DISMISS -> Not a violation
        if (action == "REJECT" || action == "DISMISS")
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                report.Status = action == "REJECT" ? "REJECTED" : "DISMISSED";
                report.ResolvedBy = staffId;
                report.ResolvedAt = now;

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }

            var resultDto = MapToReportDto(report, null);
            return ApiResponseDto<ReportListItemDto>.Ok(resultDto, $"Report has been {report.Status.ToLowerInvariant()}.");
        }

        // If action is DELETE_POST
        if (action == "DELETE_POST")
        {
            // Case 2: Validate Report is for a Post
            if (!report.ReportedPostId.HasValue || report.ReportedPostId.Value == Guid.Empty)
            {
                throw new ValidationException("Action 'DELETE_POST' is only applicable for Post reports (ReportedPostId is required).");
            }

            // Case 5: Validate reason is provided
            var deletionReason = request.EffectiveReason?.Trim();
            if (string.IsNullOrWhiteSpace(deletionReason))
            {
                throw new ValidationException("Deletion reason is required when deleting a post and cannot be empty.");
            }

            if (deletionReason.Length > 300)
            {
                throw new ValidationException("Deletion reason cannot exceed 300 characters.");
            }

            // Case 3 & 4: Retrieve post and check soft delete status
            Post? post = null;
            if (_postRepository != null)
            {
                post = await _postRepository.GetByIdAsync(report.ReportedPostId.Value, cancellationToken);
            }
            if (post == null && report.ReportedPost != null && report.ReportedPost.Id == report.ReportedPostId.Value)
            {
                post = report.ReportedPost;
            }

            if (post == null)
            {
                throw new NotFoundException($"Post with ID '{report.ReportedPostId.Value}' was not found.");
            }

            if (post.IsDeleted == true)
            {
                throw new BusinessException("The reported post has already been deleted.");
            }

            // Identify owner
            var authorId = post.AuthorId;
            report.ReportedUserId = authorId;

            var postAuthor = await _userRepository.GetByIdAsync(authorId, cancellationToken);
            if (postAuthor == null)
            {
                throw new NotFoundException("Post author not found.");
            }

            var newViolationCount = postAuthor.WarningCount + 1;

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                // 1. Soft delete post
                post.SoftDelete();
                _postRepository?.Update(post);

                // 2. Increment author violation count
                postAuthor.WarningCount = newViolationCount;
                postAuthor.UpdatedAt = now;

                // 3. Update report
                report.Status = "RESOLVED";
                report.ResolvedBy = staffId;
                report.ResolvedAt = now;
                report.NotifiedReportedUserAt = now;

                // 4. Create Notification
                var notifDescription = $"Your post has been removed because it violated FitSocial's community guidelines.\n\nReason:\n{deletionReason}\n\nPlease make sure your future posts comply with FitSocial's policies.";
                if (notifDescription.Length > 500)
                {
                    notifDescription = notifDescription.Substring(0, 497) + "...";
                }

                var notification = new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = postAuthor.UserId,
                    ActorId = staffId,
                    ReferenceId = report.ReportId,
                    Type = "ViolationPostDeleted",
                    Description = notifDescription,
                    IsRead = false,
                    CreatedAt = now
                };

                if (_notificationRepository != null)
                {
                    await _notificationRepository.AddAsync(notification, cancellationToken);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }

            // Realtime notification: broadcast PostDeleted ONLY after transaction commits successfully
            if (_postRealtimeNotifier != null)
            {
                try
                {
                    await _postRealtimeNotifier.NotifyPostDeletedAsync(post.Id);
                }
                catch
                {
                    // Failures in realtime broadcast must not break the completed business transaction
                }
            }

            var responseDto = MapToReportDto(report, null);
            return ApiResponseDto<ReportListItemDto>.Ok(responseDto, "Post deleted and report resolved successfully.");
        }

        // Action is WARN, BLOCK, or LOCK (Confirmed violation)
        Guid? reportedUserId;
        if (report.ReportedPostId.HasValue)
        {
            reportedUserId = report.ReportedPost?.AuthorId;
            if (!reportedUserId.HasValue && _postRepository != null)
            {
                var post = await _postRepository.GetByIdAsync(report.ReportedPostId.Value, cancellationToken);
                reportedUserId = post?.AuthorId;
            }
            reportedUserId ??= report.ReportedUserId;
        }
        else
        {
            reportedUserId = report.ReportedUserId;
        }

        if (!reportedUserId.HasValue || reportedUserId.Value == Guid.Empty)
        {
            throw new ValidationException("Cannot determine reported user for this report.");
        }

        report.ReportedUserId = reportedUserId.Value;

        var reportedUser = await _userRepository.GetByIdAsync(reportedUserId.Value, cancellationToken);
        if (reportedUser == null)
        {
            throw new NotFoundException("Reported user not found.");
        }

        var isCoach = string.Equals(reportedUser.RoleCode, RoleConstants.Coach, StringComparison.OrdinalIgnoreCase);
        bool hasSoldPackages = false;
        if (isCoach && _orderRepository != null)
        {
            hasSoldPackages = await _orderRepository.HasSoldTrainingPackageAsync(reportedUser.UserId, cancellationToken);
        }

        var currentViolationCount = reportedUser.WarningCount;
        var nextViolationCount = currentViolationCount + 1;

        // Business Rule validation:
        // Lock and Block are only allowed when violation count > 2 (i.e. nextViolationCount >= 3)
        if (action == "LOCK" || action == "BLOCK")
        {
            if (nextViolationCount <= 2)
            {
                throw new ValidationException(
                    $"Action '{action}' is only allowed when user has more than 2 violations (violation count > 2). " +
                    $"Current violation count is {currentViolationCount}. Processing this report would result in violation count {nextViolationCount}. " +
                    $"Only 'WARN' is permitted for this violation level.");
            }
        }

        // Special rule for Coach:
        // If Coach has sold Training Packages to Trainee -> CANNOT LOCK account. Only BLOCK is allowed.
        if (isCoach && hasSoldPackages && action == "LOCK")
        {
            throw new BusinessException(
                "Cannot lock Coach account because this Coach has sold Training Packages to Trainees. " +
                "Locking would disrupt ongoing trainee packages. Only 'BLOCK' action is permitted.");
        }

        // Execute changes atomically inside transaction
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // 1. Increment violation count
            reportedUser.WarningCount = nextViolationCount;
            reportedUser.UpdatedAt = now;

            // 2. Apply moderation action
            if (action == "LOCK")
            {
                reportedUser.IsLocked = true;
                reportedUser.LockedBy = staffId;
                reportedUser.TokenVersion += 1;
            }
            else if (action == "BLOCK")
            {
                if (isCoach)
                {
                    if (_coachProfileRepository != null)
                    {
                        var coachProfile = await _coachProfileRepository.GetByIdAsync(reportedUser.UserId, cancellationToken);
                        if (coachProfile != null)
                        {
                            coachProfile.Status = "BLOCKED";
                            coachProfile.UpdatedAt = now;
                        }
                    }

                    // If coach has NOT sold packages, lock account as well
                    if (!hasSoldPackages)
                    {
                        reportedUser.IsLocked = true;
                        reportedUser.LockedBy = staffId;
                        reportedUser.TokenVersion += 1;
                    }
                    // If coach HAS sold packages, reportedUser.IsLocked remains false (unlocked for package fulfillment)
                }
                else
                {
                    reportedUser.IsLocked = true;
                    reportedUser.LockedBy = staffId;
                    reportedUser.TokenVersion += 1;
                }
            }

            // 3. Update Report
            report.Status = "RESOLVED";
            report.ResolvedBy = staffId;
            report.ResolvedAt = now;
            report.NotifiedReportedUserAt = now;

            // 4. Create Notification
            string notifType = action switch
            {
                "WARN" => "ViolationWarning",
                "LOCK" => "ViolationLock",
                "BLOCK" => "ViolationBlock",
                _ => "ViolationReport"
            };

            string notifDescription;
            if (action == "WARN")
            {
                if (nextViolationCount == 1)
                {
                    notifDescription = "Your account has received a warning due to a reported violation.\nCurrent violation count: 1.\nPlease make sure your future activities comply with FitSocial's policies.";
                }
                else if (nextViolationCount == 2)
                {
                    notifDescription = "Your account has received another warning due to a reported violation.\nCurrent violation count: 2.\nFurther violations may result in account restrictions.";
                }
                else
                {
                    notifDescription = $"Your account has received another warning due to a reported violation.\nCurrent violation count: {nextViolationCount}.\nFurther violations may result in account restrictions.";
                }
            }
            else if (action == "LOCK")
            {
                notifDescription = $"Your account has been locked due to exceeding the allowed number of violations.\nCurrent violation count: {nextViolationCount}.";
            }
            else // BLOCK
            {
                if (isCoach && hasSoldPackages)
                {
                    notifDescription = "Your account has been blocked due to exceeding the allowed number of violations.\nBecause you currently have active Training Packages, your account cannot be locked.";
                }
                else
                {
                    notifDescription = $"Your account has been blocked due to exceeding the allowed number of violations.\nCurrent violation count: {nextViolationCount}.";
                }
            }

            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = reportedUser.UserId,
                ActorId = staffId,
                ReferenceId = report.ReportId,
                Type = notifType,
                Description = notifDescription,
                IsRead = false,
                CreatedAt = now
            };

            if (_notificationRepository != null)
            {
                await _notificationRepository.AddAsync(notification, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        var dto = MapToReportDto(report, hasSoldPackages);
        return ApiResponseDto<ReportListItemDto>.Ok(dto, $"Violation report processed successfully with action '{action}'.");
    }

    private static ReportListItemDto MapToReportDto(Report r, bool? hasSoldPackages = null)
    {
        string targetType;
        Guid? targetId;

        if (r.ReportedPostId.HasValue)
        {
            targetType = "POST";
            targetId = r.ReportedPostId.Value;
        }
        else if (r.ReportedUserId.HasValue)
        {
            targetType = "USER";
            targetId = r.ReportedUserId.Value;
        }
        else
        {
            targetType = r.Type ?? "UNKNOWN";
            targetId = null;
        }

        var targetUser = r.ReportedPostId.HasValue
            ? (r.ReportedPost?.Author ?? r.ReportedUser)
            : (r.ReportedUser ?? r.ReportedPost?.Author);
        var role = targetUser?.RoleCode ?? (targetUser != null ? "USER" : null);
        var violationCount = targetUser?.WarningCount ?? 0;
        var isLocked = targetUser?.IsLocked;

        var availableActions = new List<string>();
        var statusUpper = (r.Status ?? "PENDING").Trim().ToUpperInvariant();
        if (statusUpper == "PENDING" || statusUpper == "REVIEWED")
        {
            var nextViolationCount = violationCount + 1;
            if (nextViolationCount <= 2)
            {
                availableActions.Add("WARN");
            }
            else // nextViolationCount > 2
            {
                var isCoach = string.Equals(role, RoleConstants.Coach, StringComparison.OrdinalIgnoreCase);
                if (isCoach && hasSoldPackages == true)
                {
                    availableActions.Add("BLOCK");
                }
                else
                {
                    availableActions.Add("LOCK");
                    availableActions.Add("BLOCK");
                }
            }
            // If report is for a post and post is not already deleted, DELETE_POST is available
            if (r.ReportedPostId.HasValue && (r.ReportedPost == null || r.ReportedPost.IsDeleted != true))
            {
                availableActions.Add("DELETE_POST");
            }

            availableActions.Add("REJECT");
            availableActions.Add("DISMISS");
        }

        return new ReportListItemDto
        {
            ReportId = r.ReportId,
            ReporterId = r.ReporterId,
            ReporterName = r.Reporter?.FullName ?? r.Reporter?.Email,
            ReporterEmail = r.Reporter?.Email,
            ReporterAvatarUrl = r.Reporter?.AvatarUrl,

            TargetType = targetType,
            TargetId = targetId,

            ReportedUserId = r.ReportedPostId.HasValue
                ? (r.ReportedPost?.AuthorId ?? r.ReportedUserId)
                : (r.ReportedUserId ?? r.ReportedPost?.AuthorId),
            ReportedUserName = targetUser?.FullName ?? targetUser?.Email,
            ReportedUserEmail = targetUser?.Email,
            ReportedUserAvatarUrl = targetUser?.AvatarUrl,

            ReportedUserRole = role,
            ReportedUserViolationCount = violationCount,
            ReportedUserIsLocked = isLocked,
            HasSoldTrainingPackage = hasSoldPackages,
            AvailableActions = availableActions,

            ReportedPostId = r.ReportedPostId,
            ReportedPostContent = r.ReportedPost?.Content,
            ReportedPostAuthorId = r.ReportedPost?.AuthorId,
            ReportedPostAuthorName = r.ReportedPost?.Author?.FullName ?? r.ReportedPost?.Author?.Email,

            Type = r.Type,
            Reason = r.Reason,
            Description = r.Description,
            Status = r.Status ?? "PENDING",

            ResolvedBy = r.ResolvedBy,
            ResolverName = r.ResolvedByNavigation?.FullName ?? r.ResolvedByNavigation?.Email,
            ResolvedAt = r.ResolvedAt,

            AppealStatus = r.AppealStatus,
            AppealContent = r.AppealContent,
            AppealedAt = r.AppealedAt,
            AppealReviewedBy = r.AppealReviewedBy,
            AppealResolverName = r.AppealReviewedByNavigation?.FullName ?? r.AppealReviewedByNavigation?.Email,
            AppealReviewedAt = r.AppealReviewedAt,
            AppealReviewNote = r.AppealReviewNote,
            NotifiedReportedUserAt = r.NotifiedReportedUserAt,

            Media = r.ReportMedia != null
                ? r.ReportMedia.OrderBy(m => m.SortOrder).Select(m => new ReportMediaItemDto
                {
                    MediaId = m.MediaId,
                    MediaUrl = m.MediaUrl,
                    MediaType = m.MediaType,
                    MediaFor = m.MediaFor,
                    SortOrder = m.SortOrder
                }).ToList()
                : new List<ReportMediaItemDto>(),

            CreatedAt = r.CreatedAt
        };
    }
}

