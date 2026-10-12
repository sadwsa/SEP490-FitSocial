using System;
using System.ComponentModel.DataAnnotations;
using FitSocial.Application.DTOs.Common;

namespace FitSocial.Application.DTOs.CoachCertificate;

public class CoachCertificateAdminItemDto
{
    public Guid CertificateId { get; set; }
    public Guid CoachId { get; set; }
    public string? CoachName { get; set; }
    public string? CoachEmail { get; set; }
    public string? CoachPhoneNumber { get; set; }
    public string? CoachAvatarUrl { get; set; }

    public string? CertificateName { get; set; }
    public string? CertificateUrl { get; set; }
    public string? IssuedBy { get; set; }
    public DateOnly? IssuedDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }

    public string VerificationStatus { get; set; } = "PENDING";
    public Guid? VerifiedBy { get; set; }
    public string? VerifiedByName { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? RejectedReason { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CoachCertificateAdminDetailDto : CoachCertificateAdminItemDto
{
    public string? CoachBio { get; set; }
    public int? CoachExperienceYears { get; set; }
    public string? CoachApprovalStatus { get; set; }
    public string? VerifiedByEmail { get; set; }
}

public class CoachCertificateAdminCountsDto
{
    public int All { get; set; }
    public int Pending { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
}

public class CoachCertificateAdminListResponseDto
{
    public PagedResultDto<CoachCertificateAdminItemDto> Certificates { get; set; } = new();
    public CoachCertificateAdminCountsDto StatusCounts { get; set; } = new();
}

public class RejectCoachCertificateDto
{
    [Required(ErrorMessage = "Rejection reason is required.")]
    [StringLength(1000, ErrorMessage = "Rejection reason cannot exceed 1000 characters.")]
    public string Reason { get; set; } = string.Empty;
}

public class GetCoachCertificatesAdminQueryDto
{
    public string? Search { get; set; }
    public string? Status { get; set; } = "ALL";
    public string? SortBy { get; set; } = "date_desc";
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
