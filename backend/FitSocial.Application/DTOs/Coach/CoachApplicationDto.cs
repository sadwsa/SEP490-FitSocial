using System;
using System.Collections.Generic;
using FitSocial.Application.DTOs.Common;

namespace FitSocial.Application.DTOs.Coach;

public class CoachApplicationItemDto
{
    public Guid CoachId { get; set; }
    public string ApplicationCode => $"#APP-{CoachId.ToString()[..8].ToUpperInvariant()}";
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public int? ExperienceYears { get; set; }
    public string? Bio { get; set; }
    public string? IdentityCardUrl { get; set; }
    public string? CertificateUrl { get; set; }
    public int CertificatesCount { get; set; }
    public string? ApprovalStatus { get; set; }
    public string? Status { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? ApprovedByName { get; set; }
    public List<string> Locations { get; set; } = new();
}

public class CoachApplicationDetailDto : CoachApplicationItemDto
{
    public string? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public List<CoachCertificateDetailDto> Certificates { get; set; } = new();
    public CoachApplicationEkycDto? Ekyc { get; set; }
}

public class CoachApplicationEkycDto
{
    public string? FullNameOnCard { get; set; }
    public DateOnly? DateOfBirthOnCard { get; set; }
    public string? Sex { get; set; }
    public string? FrontCardUrl { get; set; }
    public string? BackCardUrl { get; set; }
    public string? FaceImageUrl { get; set; }
    public decimal? LivenessScore { get; set; }
    public decimal? FaceMatchConfidence { get; set; }
    public string? VerificationStatus { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class CoachApplicationStatusCountsDto
{
    public int All { get; set; }
    public int Pending { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
}

public class CoachApplicationListResponseDto
{
    public PagedResultDto<CoachApplicationItemDto> Applications { get; set; } = new();
    public CoachApplicationStatusCountsDto StatusCounts { get; set; } = new();
}

public class ApproveCoachApplicationRequestDto
{
    public string? Note { get; set; }
}

public class RejectCoachApplicationRequestDto
{
    public string? Reason { get; set; }
}

