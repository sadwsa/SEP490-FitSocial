using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Reports;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Application.Services;

public class ReportService : IReportService
{
    private readonly IReportRepository _reportRepository;

    public ReportService(IReportRepository reportRepository)
    {
        _reportRepository = reportRepository;
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
                cancellationToken: cancellationToken);

            var dtos = items.Select(r =>
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
                    targetType = "UNKNOWN";
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

                    Reason = r.Reason,
                    Status = r.Status ?? "PENDING",

                    ResolvedBy = r.ResolvedBy,
                    ResolverName = r.ResolvedByNavigation?.FullName ?? r.ResolvedByNavigation?.Email,
                    ResolvedAt = r.ResolvedAt,

                    CreatedAt = r.CreatedAt
                };
            }).ToList();

            var pagedResult = PagedResultDto<ReportListItemDto>.Create(dtos, totalCount, pageNumber, pageSize);
            return ApiResponseDto<PagedResultDto<ReportListItemDto>>.Ok(pagedResult, "Reports retrieved successfully.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<PagedResultDto<ReportListItemDto>>.Fail($"Error retrieving reports: {ex.Message}");
        }
    }
}
