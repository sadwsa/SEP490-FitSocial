using System;
using FitSocial.Client.Models.Common;

namespace FitSocial.Client.Models.CoachCertificate;

public class CoachCertificateItemModel
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

public class CoachCertificateDetailModel : CoachCertificateItemModel
{
    public string? CoachBio { get; set; }
    public int? CoachExperienceYears { get; set; }
    public string? CoachApprovalStatus { get; set; }
    public string? VerifiedByEmail { get; set; }
}

public class CoachCertificateStatusCountsModel
{
    public int All { get; set; }
    public int Pending { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
}

public class CoachCertificateListResponseModel
{
    public PagedResult<CoachCertificateItemModel> Certificates { get; set; } = new();
    public CoachCertificateStatusCountsModel StatusCounts { get; set; } = new();
}

public class RejectCoachCertificateModel
{
    public string Reason { get; set; } = string.Empty;
}

public class GetCoachCertificatesQueryModel
{
    public string? Search { get; set; }
    public string? Status { get; set; } = "ALL";
    public string? SortBy { get; set; } = "date_desc";
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
