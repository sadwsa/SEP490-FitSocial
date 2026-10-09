using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.TrainingPackage;

public class CreatePackageReviewDto
{
    [Required]
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
    public int Rating { get; set; } = 5;

    [MaxLength(1000, ErrorMessage = "Comment cannot exceed 1000 characters.")]
    public string? Comment { get; set; }
}

public class ReplyReviewDto
{
    [Required(ErrorMessage = "Reply content cannot be empty.")]
    [MaxLength(1000, ErrorMessage = "Reply cannot exceed 1000 characters.")]
    public string Reply { get; set; } = string.Empty;
}

public class PackageReviewDto
{
    public Guid ReviewId { get; set; }
    public Guid PackageId { get; set; }
    public Guid TraineeId { get; set; }
    public string TraineeName { get; set; } = "FitSocial Member";
    public string? TraineeAvatarUrl { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string? Reply { get; set; }
    public string? CoachName { get; set; }
    public string? CoachAvatarUrl { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsEdited { get; set; }
}

public class PackageReviewSummaryDto
{
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public Dictionary<int, int> RatingCounts { get; set; } = new()
    {
        { 5, 0 }, { 4, 0 }, { 3, 0 }, { 2, 0 }, { 1, 0 }
    };
    public List<PackageReviewDto> Reviews { get; set; } = new();
}
