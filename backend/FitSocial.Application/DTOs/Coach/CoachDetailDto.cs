using System;
using System.Collections.Generic;
using FitSocial.Application.DTOs.TrainingPackage;

namespace FitSocial.Application.DTOs.Coach;

public class CoachDetailDto
{
    public Guid CoachId { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }

    public int? ExperienceYears { get; set; }
    public string? Bio { get; set; }
    public string? ApprovalStatus { get; set; }
    public string? Status { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public double Rating { get; set; }
    public int TotalReviews { get; set; }

    public List<CoachLocationDetailDto> Locations { get; set; } = new();
    public List<CoachCertificateDetailDto> Certificates { get; set; } = new();
    public List<TrainingPackageResponseDto> Packages { get; set; } = new();
}

public class CoachLocationDetailDto
{
    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public string? Address { get; set; }
}

public class CoachCertificateDetailDto
{
    public Guid CertificateId { get; set; }
    public string? CertificateName { get; set; }
    public string? CertificateUrl { get; set; }
    public string? IssuedBy { get; set; }
    public DateOnly? IssuedDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string? VerificationStatus { get; set; }
}

public class UpdateCoachProfileDto
{
    public int? ExperienceYears { get; set; }
    public string? Bio { get; set; }
    public string? Status { get; set; } // ACTIVE, INACTIVE
    public List<Guid>? LocationIds { get; set; }
}
