using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Posts;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace FitSocial.Application.Services;

public class PostReportService : IPostReportService
{
    private readonly IPostReportRepository _reportRepository;
    private readonly IPostRepository _postRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PostReportService>? _logger;

    public PostReportService(
        IPostReportRepository reportRepository,
        IPostRepository postRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<PostReportService>? logger = null)
    {
        _reportRepository = reportRepository;
        _postRepository = postRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Checks whether the user has already submitted a pending report for this specific post.
    /// Check is scoped strictly to (postId, reporterId).
    /// </summary>
    public async Task<ApiResponseDto<bool>> HasUserReportedPostAsync(
        Guid postId,
        Guid reporterId,
        CancellationToken cancellationToken = default)
    {
        if (postId == Guid.Empty || reporterId == Guid.Empty)
        {
            return ApiResponseDto<bool>.Ok(false);
        }

        var hasReported = await _reportRepository.HasActiveReportAsync(postId, reporterId, cancellationToken);
        return ApiResponseDto<bool>.Ok(hasReported);
    }

    public async Task<ApiResponseDto<PostReportResponseDto>> ReportPostAsync(
        Guid postId,
        Guid reporterId,
        CreatePostReportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        // 1. Authentication check
        if (reporterId == Guid.Empty)
        {
            throw new ValidationException("User is not authenticated.");
        }

        if (postId == Guid.Empty)
        {
            throw new ValidationException("Post ID is required.");
        }

        // 2. Validation on Reason
        if (request == null || string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ValidationException("Report reason is required and cannot be empty.");
        }

        var trimmedReason = request.Reason.Trim();
        if (trimmedReason.Length > 500)
        {
            throw new ValidationException("Report reason cannot exceed 500 characters.");
        }

        // 3. Check reporter account status
        var reporter = await _userRepository.GetByIdAsync(reporterId, cancellationToken);
        if (reporter == null)
        {
            throw new NotFoundException("Reporter account was not found.");
        }

        if (reporter.IsLocked == true)
        {
            throw new ForbiddenException("Your account is locked and cannot report posts.");
        }

        // 4. Check post exists and not deleted
        var post = await _postRepository.GetByIdAsync(postId, cancellationToken);
        if (post == null)
        {
            throw new NotFoundException($"Post with ID '{postId}' was not found.");
        }

        if (post.IsDeleted == true)
        {
            throw new BusinessException("Cannot report a post that has been deleted.");
        }

        // 5. Cannot report own post
        if (post.AuthorId == reporterId)
        {
            throw new BusinessException("You cannot report your own post.");
        }

        // 6. Duplicate-report check: Scoped strictly to ReporterId + PostId.
        // It does NOT use ReporterId + PostOwnerId.
        // A report on Post 1 from Author B does NOT prevent reporting Post 2 from Author B.
        var hasActiveReport = await _reportRepository.HasActiveReportAsync(postId, reporterId, cancellationToken);
        if (hasActiveReport)
        {
            throw new ConflictException("You have already submitted a pending report for this post.");
        }

        // 7. Create and persist Report
        var report = Report.CreatePostReport(reporterId, postId, post.AuthorId, trimmedReason);

        await _reportRepository.AddAsync(report, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("User {ReporterId} successfully reported Post {PostId}. ReportId: {ReportId}",
            reporterId, postId, report.ReportId);

        var responseDto = new PostReportResponseDto
        {
            ReportId = report.ReportId,
            PostId = postId,
            ReporterId = reporterId,
            Reason = report.Reason ?? string.Empty,
            Status = report.Status ?? string.Empty,
            CreatedAt = report.CreatedAt
        };

        return ApiResponseDto<PostReportResponseDto>.Ok(responseDto, "Post reported successfully.");
    }
}
