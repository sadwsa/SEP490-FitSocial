using System;

namespace FitSocial.Domain.Entities;

public partial class CoachEkycVerification
{
    public Guid EkycId { get; set; }

    public Guid CoachId { get; set; }

    public string? IdCardNumber { get; set; }

    public string? FullNameOnCard { get; set; }

    public DateOnly? DateOfBirthOnCard { get; set; }

    public string? Birthplace { get; set; }

    public string? Sex { get; set; }

    public string? Address { get; set; }

    public string? Province { get; set; }

    public string? District { get; set; }

    public string? Ward { get; set; }

    public string? ProvinceCode { get; set; }

    public string? DistrictCode { get; set; }

    public string? WardCode { get; set; }

    public string? Street { get; set; }

    public string? Nationality { get; set; }

    public string? Religion { get; set; }

    public string? Ethnicity { get; set; }

    public string? Expiry { get; set; }

    public string? Feature { get; set; }

    public string? IssueDate { get; set; }

    public string? IssueBy { get; set; }

    public string? DocumentType { get; set; }

    public decimal? LivenessScore { get; set; }

    public decimal? FaceMatchConfidence { get; set; }

    public string? RawInformationJson { get; set; }

    public string FrontCardUrl { get; set; } = null!;

    public string BackCardUrl { get; set; } = null!;

    public string? FaceImageUrl { get; set; }

    public string? VerificationStatus { get; set; }

    public string? FailureReason { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual CoachProfile Coach { get; set; } = null!;
}
