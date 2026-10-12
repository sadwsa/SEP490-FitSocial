using System;
using System.Collections.Generic;
using System.Linq;

namespace FitSocial.Client.Models.TrainingPackages;

public class TrainingPackageMediaDto
{
    public Guid MediaId { get; set; }
    public Guid PackageId { get; set; }
    public string MediaUrl { get; set; } = string.Empty;
    public string? MediaType { get; set; } = "IMAGE";
    public short? SortOrder { get; set; } = 0;
    public DateTime? CreatedAt { get; set; }
}

public class TrainingPackageResponseDto
{
    public Guid PackageId { get; set; }
    public Guid CoachId { get; set; }
    public string? CoachName { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public short? SessionCount { get; set; }
    public short? MinAge { get; set; }
    public string? TargetAudience { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<TrainingPackageMediaDto> Media { get; set; } = new();
    public List<string> ImageUrls => Media?.OrderBy(m => m.SortOrder).Select(m => m.MediaUrl).ToList() ?? new List<string>();
    public string? ThumbnailUrl => ImageUrls.FirstOrDefault();

    // Review state of the current trainee
    public bool HasReviewed { get; set; }
    public bool IsReviewEdited { get; set; }
    public int? UserRating { get; set; }
    public string? UserComment { get; set; }
}
