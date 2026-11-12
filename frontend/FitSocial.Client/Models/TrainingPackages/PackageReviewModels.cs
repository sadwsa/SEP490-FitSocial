using System;
using System.Collections.Generic;

namespace FitSocial.Client.Models.TrainingPackages;

public class CreatePackageReviewDto
{
    public Guid OrderId { get; set; }
    public int Rating { get; set; } = 5;
    public string? Comment { get; set; }
    public List<string> ImageUrls { get; set; } = new();
}

public class ReplyReviewDto
{
    public string Reply { get; set; } = string.Empty;
}

public class ReviewMediaDto
{
    public Guid ReviewMediaId { get; set; }
    public string MediaUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = "image";
    public int SortOrder { get; set; }
}

public class PackageReviewDto
{
    public Guid ReviewId { get; set; }
    public Guid PackageId { get; set; }
    public Guid TraineeId { get; set; }
    public string TraineeName { get; set; } = "FitConnect Member";
    public string? TraineeAvatarUrl { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string? Reply { get; set; }
    public string? CoachName { get; set; }
    public string? CoachAvatarUrl { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsEdited { get; set; }
    public List<ReviewMediaDto> Media { get; set; } = new();
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
