using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Appeals;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FitSocial.Application.Services;

public class AppealService : IAppealService
{
    private readonly IAppealRepository _appealRepository;
    private readonly IReportRepository _reportRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AppealService> _logger;

    public AppealService(
        IAppealRepository appealRepository,
        IReportRepository reportRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<AppealService> logger)
    {
        _appealRepository = appealRepository;
        _reportRepository = reportRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ApiResponseDto<AppealDetailDto>> SubmitAppealAsync(
        Guid? currentUserId,
        SubmitAppealRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ValidationException("Appeal request cannot be empty.");
        }

        var effectiveUserId = ResolveEffectiveUserId(currentUserId, request.AppealToken);

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 3 || reason.Length > 500)
        {
            throw new ValidationException("Appeal reason must be between 3 and 500 characters.");
        }

        var content = request.Content?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(content) || content.Length < 10 || content.Length > 2000)
        {
            throw new ValidationException("Appeal explanation content must be between 10 and 2000 characters.");
        }

        if (request.ReportId == Guid.Empty)
        {
            throw new ValidationException("A valid Report ID is required.");
        }

        var user = await _userRepository.GetByIdAsync(effectiveUserId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("User account not found.");
        }

        var role = user.RoleCode?.Trim().ToUpperInvariant();
        if (role != RoleConstants.Trainee && role != RoleConstants.Coach)
        {
            throw new BusinessException("Only Trainees and Coaches are allowed to submit appeals.");
        }

        var report = await _reportRepository.GetReportWithDetailsByIdAsync(request.ReportId, cancellationToken);
        if (report == null)
        {
            throw new NotFoundException("Report not found.");
        }

        // Ownership verification: Only the reported user or author of the reported post can appeal
        var isReportedUser = (report.ReportedUserId.HasValue && report.ReportedUserId.Value == effectiveUserId)
            || (report.ReportedPost != null && report.ReportedPost.AuthorId == effectiveUserId);

        if (!isReportedUser)
        {
            throw new ForbiddenException("You can only appeal reports that were filed against your own account or content.");
        }

        // Check if an active appeal already exists
        var hasActiveAppeal = await _appealRepository.HasActiveAppealForReportAsync(report.ReportId, cancellationToken);
        if (hasActiveAppeal || report.AppealStatus == "PENDING" || report.AppealStatus == "APPROVED")
        {
            throw new BusinessException("An appeal has already been submitted for this report and is currently being processed or has been approved.");
        }

        // Evidence validation
        var cleanEvidenceUrls = new List<string>();
        if (request.EvidenceUrls != null && request.EvidenceUrls.Any())
        {
            foreach (var rawUrl in request.EvidenceUrls)
            {
                if (string.IsNullOrWhiteSpace(rawUrl)) continue;
                var trimmed = rawUrl.Trim();
                if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    throw new ValidationException($"Invalid evidence URL: '{trimmed}'.");
                }
                cleanEvidenceUrls.Add(trimmed);
            }

            cleanEvidenceUrls = cleanEvidenceUrls.Distinct().ToList();

            if (cleanEvidenceUrls.Count > 10)
            {
                throw new ValidationException("You can upload a maximum of 10 evidence files.");
            }
        }

        var now = DateTime.UtcNow;
        var appeal = new Appeal
        {
            AppealId = Guid.NewGuid(),
            ReportId = report.ReportId,
            AppealerId = effectiveUserId,
            Reason = reason,
            Content = content,
            Status = "PENDING",
            CreatedAt = now,
            UpdatedAt = now
        };

        short sortOrder = 0;
        foreach (var url in cleanEvidenceUrls)
        {
            var isVideo = url.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                       || url.EndsWith(".mov", StringComparison.OrdinalIgnoreCase)
                       || url.EndsWith(".avi", StringComparison.OrdinalIgnoreCase)
                       || url.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase)
                       || url.EndsWith(".webm", StringComparison.OrdinalIgnoreCase)
                       || url.Contains("/video/upload/", StringComparison.OrdinalIgnoreCase);

            var isPdf = url.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
            var mediaType = isVideo ? "VIDEO" : (isPdf ? "PDF" : "IMAGE");

            appeal.AppealMedia.Add(new AppealMedium
            {
                MediaId = Guid.NewGuid(),
                AppealId = appeal.AppealId,
                MediaUrl = url,
                MediaType = mediaType,
                SortOrder = sortOrder++,
                CreatedAt = now
            });
        }

        // Also update Report fields to keep in sync with legacy queries
        report.AppealStatus = "PENDING";
        report.AppealContent = content;
        report.AppealedAt = now;

        await _appealRepository.AddAsync(appeal, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appeal {AppealId} successfully created for Report {ReportId} by User {UserId}",
            appeal.AppealId, report.ReportId, effectiveUserId);

        var appealDetail = await _appealRepository.GetByIdWithDetailsAsync(appeal.AppealId, cancellationToken);
        return ApiResponseDto<AppealDetailDto>.Ok(MapToAppealDetailDto(appealDetail ?? appeal, report), "Your appeal has been submitted successfully.");
    }

    public async Task<ApiResponseDto<List<AppealableReportDto>>> GetAppealableReportsAsync(
        Guid? currentUserId,
        string? appealToken,
        CancellationToken cancellationToken = default)
    {
        var effectiveUserId = ResolveEffectiveUserId(currentUserId, appealToken);

        // Fetch user's reports (reports where ReportedUserId == effectiveUserId or ReportedPost.AuthorId == effectiveUserId)
        var (allReports, _) = await _reportRepository.GetPagedReportsAsync(
            status: null,
            targetType: null,
            reporterId: null,
            fromDate: null,
            toDate: null,
            searchTerm: null,
            pageNumber: 1,
            pageSize: 100,
            cancellationToken: cancellationToken);

        var myReports = allReports
            .Where(r => (r.ReportedUserId.HasValue && r.ReportedUserId.Value == effectiveUserId)
                     || (r.ReportedPost != null && r.ReportedPost.AuthorId == effectiveUserId))
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

        var dtos = new List<AppealableReportDto>();
        foreach (var r in myReports)
        {
            var existingAppeal = await _appealRepository.GetByReportIdAsync(r.ReportId, cancellationToken);
            dtos.Add(MapToAppealableReportDto(r, existingAppeal));
        }

        return ApiResponseDto<List<AppealableReportDto>>.Ok(dtos, "Reports retrieved successfully.");
    }

    public async Task<ApiResponseDto<AppealableReportDto>> GetReportForAppealAsync(
        Guid reportId,
        Guid? currentUserId,
        string? appealToken,
        CancellationToken cancellationToken = default)
    {
        var effectiveUserId = ResolveEffectiveUserId(currentUserId, appealToken);

        var report = await _reportRepository.GetReportWithDetailsByIdAsync(reportId, cancellationToken);
        if (report == null)
        {
            throw new NotFoundException("Report not found.");
        }

        var isOwner = (report.ReportedUserId.HasValue && report.ReportedUserId.Value == effectiveUserId)
            || (report.ReportedPost != null && report.ReportedPost.AuthorId == effectiveUserId);

        if (!isOwner)
        {
            throw new ForbiddenException("You do not have permission to view details of this report.");
        }

        var existingAppeal = await _appealRepository.GetByReportIdAsync(report.ReportId, cancellationToken);
        return ApiResponseDto<AppealableReportDto>.Ok(MapToAppealableReportDto(report, existingAppeal), "Report details retrieved successfully.");
    }

    public async Task<ApiResponseDto<AppealDetailDto>> GetAppealDetailAsync(
        Guid appealId,
        Guid? currentUserId,
        string? appealToken,
        CancellationToken cancellationToken = default)
    {
        var effectiveUserId = ResolveEffectiveUserId(currentUserId, appealToken);

        var appeal = await _appealRepository.GetByIdWithDetailsAsync(appealId, cancellationToken);
        if (appeal == null)
        {
            throw new NotFoundException("Appeal not found.");
        }

        if (appeal.AppealerId != effectiveUserId)
        {
            throw new ForbiddenException("You do not have permission to view this appeal.");
        }

        return ApiResponseDto<AppealDetailDto>.Ok(MapToAppealDetailDto(appeal, appeal.Report), "Appeal details retrieved successfully.");
    }

    public string GenerateAppealToken(User user)
    {
        var secret = GetSecretKey();
        var expSeconds = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds();
        var payload = $"{user.UserId}:{expSeconds}:BLOCKED_APPEAL";
        var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadBase64));
        var signatureBase64 = Convert.ToBase64String(hash);

        return $"{payloadBase64}.{signatureBase64}";
    }

    public Guid ValidateAppealToken(string appealToken)
    {
        if (string.IsNullOrWhiteSpace(appealToken))
        {
            throw new ValidationException("Appeal token is required for blocked account submission.");
        }

        var parts = appealToken.Trim().Split('.');
        if (parts.Length != 2)
        {
            throw new ValidationException("Invalid appeal token format.");
        }

        var payloadBase64 = parts[0];
        var signatureBase64 = parts[1];
        var secret = GetSecretKey();

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expectedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadBase64));
        var expectedSignatureBase64 = Convert.ToBase64String(expectedHash);

        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signatureBase64),
            Encoding.UTF8.GetBytes(expectedSignatureBase64)))
        {
            throw new ValidationException("Invalid or forged appeal token.");
        }

        string decodedPayload;
        try
        {
            decodedPayload = Encoding.UTF8.GetString(Convert.FromBase64String(payloadBase64));
        }
        catch
        {
            throw new ValidationException("Corrupted appeal token payload.");
        }

        var payloadParts = decodedPayload.Split(':');
        if (payloadParts.Length != 3)
        {
            throw new ValidationException("Malformed appeal token payload.");
        }

        if (!Guid.TryParse(payloadParts[0], out var userId))
        {
            throw new ValidationException("Invalid User ID in appeal token.");
        }

        if (!long.TryParse(payloadParts[1], out var expSeconds))
        {
            throw new ValidationException("Invalid expiration timestamp in appeal token.");
        }

        var purpose = payloadParts[2];
        if (purpose != "BLOCKED_APPEAL")
        {
            throw new ValidationException("Invalid appeal token purpose.");
        }

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expSeconds)
        {
            throw new ValidationException("Appeal token has expired. Please sign in again to receive a fresh token.");
        }

        return userId;
    }

    private Guid ResolveEffectiveUserId(Guid? currentUserId, string? appealToken)
    {
        if (currentUserId.HasValue && currentUserId.Value != Guid.Empty)
        {
            return currentUserId.Value;
        }

        if (!string.IsNullOrWhiteSpace(appealToken))
        {
            return ValidateAppealToken(appealToken);
        }

        throw new UnauthorizedAccessException("Authentication or a valid appeal token is required.");
    }

    private string GetSecretKey()
    {
        return _configuration["Jwt:SecretKey"] ?? "FitSocial_Super_Secret_Key_For_Jwt_2026_SecureAuthenticationKey_1234567890";
    }

    private static AppealableReportDto MapToAppealableReportDto(Report report, Appeal? existingAppeal)
    {
        var hasActive = existingAppeal != null && (existingAppeal.Status == "PENDING" || existingAppeal.Status == "APPROVED");
        var postSnippet = report.ReportedPost?.Content;
        if (!string.IsNullOrWhiteSpace(postSnippet) && postSnippet.Length > 100)
        {
            postSnippet = postSnippet.Substring(0, 97) + "...";
        }

        return new AppealableReportDto
        {
            ReportId = report.ReportId,
            Type = report.Type,
            Reason = report.Reason,
            Description = report.Description,
            Status = report.Status,
            ReportedPostId = report.ReportedPostId,
            ReportedPostSnippet = postSnippet,
            ReportedUserId = report.ReportedUserId,
            CreatedAt = report.CreatedAt,
            ResolvedAt = report.ResolvedAt,
            HasActiveAppeal = hasActive,
            CurrentAppealStatus = existingAppeal?.Status ?? report.AppealStatus,
            ActiveAppealId = existingAppeal?.AppealId
        };
    }

    private static AppealDetailDto MapToAppealDetailDto(Appeal appeal, Report? report)
    {
        return new AppealDetailDto
        {
            AppealId = appeal.AppealId,
            ReportId = appeal.ReportId,
            AppealerId = appeal.AppealerId,
            AppealerName = appeal.Appealer?.FullName,
            AppealerAvatar = appeal.Appealer?.AvatarUrl,
            AppealerEmail = appeal.Appealer?.Email,
            Reason = appeal.Reason,
            Content = appeal.Content,
            Status = appeal.Status,
            ReviewedBy = appeal.ReviewedBy,
            ReviewedByName = appeal.ReviewedByNavigation?.FullName,
            ReviewedAt = appeal.ReviewedAt,
            ReviewNote = appeal.ReviewNote,
            CreatedAt = appeal.CreatedAt,
            UpdatedAt = appeal.UpdatedAt,
            Report = report != null ? MapToAppealableReportDto(report, appeal) : null,
            Media = appeal.AppealMedia?.Select(m => new AppealMediaDto
            {
                MediaId = m.MediaId,
                AppealId = m.AppealId,
                MediaUrl = m.MediaUrl,
                MediaType = m.MediaType,
                SortOrder = m.SortOrder,
                CreatedAt = m.CreatedAt
            }).ToList() ?? new()
        };
    }
}
