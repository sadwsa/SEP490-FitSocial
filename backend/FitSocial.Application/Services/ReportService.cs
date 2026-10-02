using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Reports;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Application.Services;

public class ReportService : IReportService
{
    private readonly IReportRepository _reportRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReportService(
        IReportRepository reportRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _reportRepository = reportRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
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

            var dtos = items.Select(MapToReportDto).ToList();

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

        return ApiResponseDto<ReportListItemDto>.Ok(MapToReportDto(report));
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
        return ApiResponseDto<ReportListItemDto>.Ok(MapToReportDto(detailedReport ?? report), "User reported successfully.");
    }

    private static ReportListItemDto MapToReportDto(Report r)
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

        return new ReportListItemDto
        {
            ReportId = r.ReportId,
            ReporterId = r.ReporterId,
            ReporterName = r.Reporter?.FullName ?? r.Reporter?.Email,
            ReporterEmail = r.Reporter?.Email,
            ReporterAvatarUrl = r.Reporter?.AvatarUrl,

            TargetType = targetType,
            TargetId = targetId,

            ReportedUserId = r.ReportedUserId,
            ReportedUserName = r.ReportedUser?.FullName ?? r.ReportedUser?.Email,
            ReportedUserEmail = r.ReportedUser?.Email,
            ReportedUserAvatarUrl = r.ReportedUser?.AvatarUrl,

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
