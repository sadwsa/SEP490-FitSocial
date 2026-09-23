using System;

namespace FitSocial.Domain.Entities;

public partial class CoachEkycVerification
{
    public Guid EkycId { get; set; }

    public Guid CoachId { get; set; }

    public string? IdCardNumber { get; set; }

    public string? FullNameOnCard { get; set; }

    public DateOnly? DateOfBirthOnCard { get; set; }

    public string FrontCardUrl { get; set; } = null!;

    public string BackCardUrl { get; set; } = null!;

    public string? FaceImageUrl { get; set; }

    public string? VerificationStatus { get; set; }

    public string? FailureReason { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual CoachProfile Coach { get; set; } = null!;
}
