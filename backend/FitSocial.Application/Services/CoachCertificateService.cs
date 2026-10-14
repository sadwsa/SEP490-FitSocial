using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using FitSocial.Application.DTOs.CoachCertificate;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Notifications;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Application.Services;

public class CoachCertificateService : ICoachCertificateService
{
    private readonly ICoachCertificateRepository _certificateRepository;
    private readonly ICoachProfileRepository _coachProfileRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogRepository? _auditLogs;
    private readonly INotificationRepository? _notifications;
    private readonly IUserRepository? _users;
    private readonly IChatRealtimeNotifier? _realtimeNotifier;

    public CoachCertificateService(
        ICoachCertificateRepository certificateRepository,
        ICoachProfileRepository coachProfileRepository,
        IUnitOfWork unitOfWork,
        IAuditLogRepository? auditLogs = null,
        INotificationRepository? notifications = null,
        IUserRepository? users = null,
        IChatRealtimeNotifier? realtimeNotifier = null)
    {
        _certificateRepository = certificateRepository;
        _coachProfileRepository = coachProfileRepository;
        _unitOfWork = unitOfWork;
        _auditLogs = auditLogs;
        _notifications = notifications;
        _users = users;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<ApiResponseDto<CoachCertificateAdminListResponseDto>> GetCertificatesAsync(
        GetCoachCertificatesAdminQueryDto query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (items, totalCount) = await _certificateRepository.GetPagedCertificatesAsync(
                query.Search,
                query.Status,
                query.SortBy,
                query.PageNumber,
                query.PageSize,
                cancellationToken);

            var countsDict = await _certificateRepository.GetStatusCountsAsync(cancellationToken);

            var dtos = items.Select(MapToItemDto).ToList();

            var pagedResult = PagedResultDto<CoachCertificateAdminItemDto>.Create(
                dtos,
                totalCount,
                query.PageNumber,
                query.PageSize);

            var response = new CoachCertificateAdminListResponseDto
            {
                Certificates = pagedResult,
                StatusCounts = new CoachCertificateAdminCountsDto
                {
                    All = countsDict.GetValueOrDefault("ALL", 0),
                    Pending = countsDict.GetValueOrDefault("PENDING", 0),
                    Approved = countsDict.GetValueOrDefault("APPROVED", 0),
                    Rejected = countsDict.GetValueOrDefault("REJECTED", 0)
                }
            };

            return ApiResponseDto<CoachCertificateAdminListResponseDto>.Ok(response, "Coach certificates retrieved successfully.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<CoachCertificateAdminListResponseDto>.Fail($"Error retrieving coach certificates: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<CoachCertificateAdminDetailDto>> GetCertificateByIdAsync(
        Guid certificateId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cert = await _certificateRepository.GetCertificateWithDetailsAsync(certificateId, cancellationToken);
            if (cert == null)
            {
                return ApiResponseDto<CoachCertificateAdminDetailDto>.Fail("Certificate not found.");
            }

            var detail = MapToDetailDto(cert);
            return ApiResponseDto<CoachCertificateAdminDetailDto>.Ok(detail, "Certificate details retrieved successfully.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<CoachCertificateAdminDetailDto>.Fail($"Error retrieving certificate: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<CoachCertificateAdminDetailDto>> ApproveCertificateAsync(
        Guid certificateId,
        Guid reviewerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cert = await _certificateRepository.GetCertificateWithDetailsAsync(certificateId, cancellationToken);
            if (cert == null)
            {
                return ApiResponseDto<CoachCertificateAdminDetailDto>.Fail("Certificate not found.");
            }

            var currentStatus = cert.VerificationStatus?.Trim().ToUpperInvariant() ?? "PENDING";
            if (currentStatus == "APPROVED")
            {
                return ApiResponseDto<CoachCertificateAdminDetailDto>.Fail("Certificate has already been approved.");
            }
            if (currentStatus == "REJECTED")
            {
                return ApiResponseDto<CoachCertificateAdminDetailDto>.Fail("Certificate has already been reviewed and rejected.");
            }
            if (currentStatus != "PENDING")
            {
                return ApiResponseDto<CoachCertificateAdminDetailDto>.Fail($"Certificate cannot be approved from status '{currentStatus}'.");
            }

            var now = DateTime.UtcNow;
            cert.VerificationStatus = "APPROVED";
            cert.VerifiedBy = reviewerId;
            cert.VerifiedAt = now;
            cert.UpdatedAt = now;
            cert.RejectedReason = null;
            cert.VerifiedByNavigation = null;

            _certificateRepository.Update(cert);

            // Coach notification
            var certTitle = !string.IsNullOrWhiteSpace(cert.CertificateName) ? cert.CertificateName : "Coach Certificate";
            var coachNotificationMsg = $"Your coach certificate \"{certTitle}\" has been approved.";
            if (_notifications != null)
            {
                try
                {
                    await _notifications.AddAsync(new Notification
                    {
                        Id = Guid.NewGuid(),
                        UserId = cert.CoachId,
                        ActorId = reviewerId,
                        ReferenceId = cert.CertificateId,
                        Type = "COACH_CERTIFICATE_APPROVED",
                        Description = TruncateDescription(coachNotificationMsg),
                        IsRead = false,
                        CreatedAt = now
                    }, cancellationToken);
                }
                catch
                {
                    // Non-fatal notification failure
                }
            }

            // Audit log (OldValue and NewValue must be valid JSON for jsonb column)
            if (_auditLogs != null)
            {
                try
                {
                    await _auditLogs.AddAsync(new AuditLog
                    {
                        ActorAccountId = reviewerId,
                        Action = "APPROVE_COACH_CERTIFICATE",
                        EntityType = "CoachCertificate",
                        EntityId = cert.CertificateId.ToString(),
                        OldValue = JsonSerializer.Serialize(new { status = currentStatus }),
                        NewValue = JsonSerializer.Serialize(new { status = "APPROVED", verifiedBy = reviewerId, verifiedAt = now }),
                        CreatedAt = now
                    }, cancellationToken);
                }
                catch
                {
                    // Non-fatal audit log failure
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Realtime push notification
            if (_realtimeNotifier != null)
            {
                try
                {
                    var reviewerName = await GetReviewerNameAsync(reviewerId, cancellationToken);
                    await _realtimeNotifier.SendNotificationAsync(cert.CoachId, new NotificationDto
                    {
                        NotificationId = Guid.NewGuid(),
                        Type = "COACH_CERTIFICATE_APPROVED",
                        SenderId = reviewerId,
                        SenderName = reviewerName,
                        MessagePreview = coachNotificationMsg,
                        CreatedAt = now,
                        IsRead = false
                    });
                }
                catch
                {
                    // Realtime notification failure is non-fatal
                }
            }

            // Refresh entity with reviewer navigation for response
            var refreshed = await _certificateRepository.GetCertificateWithDetailsAsync(certificateId, cancellationToken) ?? cert;
            var detail = MapToDetailDto(refreshed);
            return ApiResponseDto<CoachCertificateAdminDetailDto>.Ok(detail, "Coach certificate approved successfully.");
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException != null ? $"{ex.Message} Inner: {ex.InnerException.Message}" : ex.Message;
            return ApiResponseDto<CoachCertificateAdminDetailDto>.Fail($"Error approving certificate: {msg}");
        }
    }

    public async Task<ApiResponseDto<CoachCertificateAdminDetailDto>> RejectCertificateAsync(
        Guid certificateId,
        Guid reviewerId,
        RejectCoachCertificateDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cert = await _certificateRepository.GetCertificateWithDetailsAsync(certificateId, cancellationToken);
            if (cert == null)
            {
                return ApiResponseDto<CoachCertificateAdminDetailDto>.Fail("Certificate not found.");
            }

            var currentStatus = cert.VerificationStatus?.Trim().ToUpperInvariant() ?? "PENDING";
            if (currentStatus == "REJECTED")
            {
                return ApiResponseDto<CoachCertificateAdminDetailDto>.Fail("Certificate has already been rejected.");
            }
            if (currentStatus == "APPROVED")
            {
                return ApiResponseDto<CoachCertificateAdminDetailDto>.Fail("Certificate has already been reviewed and approved.");
            }
            if (currentStatus != "PENDING")
            {
                return ApiResponseDto<CoachCertificateAdminDetailDto>.Fail($"Certificate cannot be rejected from status '{currentStatus}'.");
            }

            if (request == null || string.IsNullOrWhiteSpace(request.Reason))
            {
                return ApiResponseDto<CoachCertificateAdminDetailDto>.Fail("Rejection reason is required.");
            }

            var reason = request.Reason.Trim();
            if (reason.Length > 1000)
            {
                return ApiResponseDto<CoachCertificateAdminDetailDto>.Fail("Rejection reason cannot exceed 1000 characters.");
            }

            var now = DateTime.UtcNow;
            cert.VerificationStatus = "REJECTED";
            cert.RejectedReason = reason;
            cert.VerifiedBy = reviewerId;
            cert.VerifiedAt = now;
            cert.UpdatedAt = now;
            cert.VerifiedByNavigation = null;

            _certificateRepository.Update(cert);

            // Notification for the certificate rejection
            var certTitle = !string.IsNullOrWhiteSpace(cert.CertificateName) ? cert.CertificateName : "Coach Certificate";
            var rejectNotificationMsg = $"Your coach certificate \"{certTitle}\" has been rejected. Reason: {reason}";
            if (_notifications != null)
            {
                try
                {
                    await _notifications.AddAsync(new Notification
                    {
                        Id = Guid.NewGuid(),
                        UserId = cert.CoachId,
                        ActorId = reviewerId,
                        ReferenceId = cert.CertificateId,
                        Type = "COACH_CERTIFICATE_REJECTED",
                        Description = TruncateDescription(rejectNotificationMsg),
                        IsRead = false,
                        CreatedAt = now
                    }, cancellationToken);
                }
                catch
                {
                    // Non-fatal
                }
            }

            // Audit log (OldValue and NewValue must be valid JSON for jsonb column)
            if (_auditLogs != null)
            {
                try
                {
                    await _auditLogs.AddAsync(new AuditLog
                    {
                        ActorAccountId = reviewerId,
                        Action = "REJECT_COACH_CERTIFICATE",
                        EntityType = "CoachCertificate",
                        EntityId = cert.CertificateId.ToString(),
                        OldValue = JsonSerializer.Serialize(new { status = currentStatus }),
                        NewValue = JsonSerializer.Serialize(new { status = "REJECTED", reason, verifiedBy = reviewerId, verifiedAt = now }),
                        CreatedAt = now
                    }, cancellationToken);
                }
                catch
                {
                    // Non-fatal
                }
            }

            // Check if all certificates of this coach are now rejected
            var profile = await _coachProfileRepository.GetCoachApplicationDetailsAsync(cert.CoachId, cancellationToken);
            if (profile != null)
            {
                var certs = profile.CoachCertificates?.ToList() ?? new List<CoachCertificate>();
                var target = certs.FirstOrDefault(c => c.CertificateId == certificateId);
                if (target != null)
                {
                    target.VerificationStatus = "REJECTED";
                    target.RejectedReason = reason;
                    target.VerifiedBy = reviewerId;
                    target.VerifiedAt = now;
                    target.UpdatedAt = now;
                    target.VerifiedByNavigation = null;
                }

                var allRejected = certs.Count > 0 && certs.All(c => string.Equals(c.VerificationStatus, "REJECTED", StringComparison.OrdinalIgnoreCase));
                var isPendingApp = string.IsNullOrWhiteSpace(profile.ApprovalStatus) || string.Equals(profile.ApprovalStatus, "PENDING", StringComparison.OrdinalIgnoreCase);

                if (allRejected && isPendingApp)
                {
                    profile.ApprovalStatus = "REJECTED";
                    profile.ApprovedBy = reviewerId;
                    profile.UpdatedAt = now;
                    if (profile.Coach != null)
                    {
                        profile.Coach.IsLocked = true;
                        profile.Coach.UpdatedAt = now;
                    }

                    if (_notifications != null)
                    {
                        try
                        {
                            await _notifications.AddAsync(new Notification
                            {
                                Id = Guid.NewGuid(),
                                UserId = profile.CoachId,
                                ActorId = reviewerId,
                                Type = "COACH_APPLICATION_REJECTED",
                                Description = TruncateDescription($"Your coach application has been automatically rejected because all certificates were rejected. Reason: {reason}"),
                                CreatedAt = now,
                                IsRead = false
                            }, cancellationToken);
                        }
                        catch
                        {
                            // Non-fatal
                        }
                    }
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Realtime push notification
            if (_realtimeNotifier != null)
            {
                try
                {
                    var reviewerName = await GetReviewerNameAsync(reviewerId, cancellationToken);
                    await _realtimeNotifier.SendNotificationAsync(cert.CoachId, new NotificationDto
                    {
                        NotificationId = Guid.NewGuid(),
                        Type = "COACH_CERTIFICATE_REJECTED",
                        SenderId = reviewerId,
                        SenderName = reviewerName,
                        MessagePreview = rejectNotificationMsg,
                        CreatedAt = now,
                        IsRead = false
                    });
                }
                catch
                {
                    // Non-fatal
                }
            }

            var refreshed = await _certificateRepository.GetCertificateWithDetailsAsync(certificateId, cancellationToken) ?? cert;
            var detail = MapToDetailDto(refreshed);
            return ApiResponseDto<CoachCertificateAdminDetailDto>.Ok(detail, "Coach certificate rejected successfully.");
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException != null ? $"{ex.Message} Inner: {ex.InnerException.Message}" : ex.Message;
            return ApiResponseDto<CoachCertificateAdminDetailDto>.Fail($"Error rejecting certificate: {msg}");
        }
    }

    private static string TruncateDescription(string? desc, int maxLength = 500)
    {
        if (string.IsNullOrEmpty(desc)) return string.Empty;
        return desc.Length <= maxLength ? desc : desc.Substring(0, maxLength);
    }

    private async Task<string> GetReviewerNameAsync(Guid reviewerId, CancellationToken cancellationToken)
    {
        if (_users != null)
        {
            try
            {
                var user = await _users.GetByIdAsync(reviewerId, cancellationToken);
                if (user != null && !string.IsNullOrWhiteSpace(user.FullName))
                {
                    return user.FullName;
                }
            }
            catch
            {
                // Fallback below
            }
        }
        return "Staff / Admin";
    }

    private static CoachCertificateAdminItemDto MapToItemDto(CoachCertificate cert)
    {
        var coachUser = cert.Coach?.Coach;
        return new CoachCertificateAdminItemDto
        {
            CertificateId = cert.CertificateId,
            CoachId = cert.CoachId,
            CoachName = coachUser?.FullName ?? "Unknown Coach",
            CoachEmail = coachUser?.Email ?? string.Empty,
            CoachPhoneNumber = coachUser?.PhoneNumber,
            CoachAvatarUrl = coachUser?.AvatarUrl,
            CertificateName = cert.CertificateName,
            CertificateUrl = cert.CertificateUrl,
            IssuedBy = cert.IssuedBy,
            IssuedDate = cert.IssuedDate,
            ExpiryDate = cert.ExpiryDate,
            VerificationStatus = cert.VerificationStatus ?? "PENDING",
            VerifiedBy = cert.VerifiedBy,
            VerifiedByName = cert.VerifiedByNavigation?.FullName,
            VerifiedAt = cert.VerifiedAt,
            RejectedReason = cert.RejectedReason,
            CreatedAt = cert.CreatedAt,
            UpdatedAt = cert.UpdatedAt
        };
    }

    private static CoachCertificateAdminDetailDto MapToDetailDto(CoachCertificate cert)
    {
        var coachUser = cert.Coach?.Coach;
        return new CoachCertificateAdminDetailDto
        {
            CertificateId = cert.CertificateId,
            CoachId = cert.CoachId,
            CoachName = coachUser?.FullName ?? "Unknown Coach",
            CoachEmail = coachUser?.Email ?? string.Empty,
            CoachPhoneNumber = coachUser?.PhoneNumber,
            CoachAvatarUrl = coachUser?.AvatarUrl,
            CertificateName = cert.CertificateName,
            CertificateUrl = cert.CertificateUrl,
            IssuedBy = cert.IssuedBy,
            IssuedDate = cert.IssuedDate,
            ExpiryDate = cert.ExpiryDate,
            VerificationStatus = cert.VerificationStatus ?? "PENDING",
            VerifiedBy = cert.VerifiedBy,
            VerifiedByName = cert.VerifiedByNavigation?.FullName,
            VerifiedAt = cert.VerifiedAt,
            RejectedReason = cert.RejectedReason,
            CreatedAt = cert.CreatedAt,
            UpdatedAt = cert.UpdatedAt,
            CoachBio = cert.Coach?.Bio,
            CoachExperienceYears = cert.Coach?.ExperienceYears,
            CoachApprovalStatus = cert.Coach?.ApprovalStatus,
            VerifiedByEmail = cert.VerifiedByNavigation?.Email
        };
    }
}
