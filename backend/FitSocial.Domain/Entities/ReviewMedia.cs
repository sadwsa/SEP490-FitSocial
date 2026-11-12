using System;

namespace FitSocial.Domain.Entities;

public class ReviewMedia
{
    public Guid ReviewMediaId { get; set; }

    public Guid ReviewId { get; set; }

    public string MediaUrl { get; set; } = string.Empty;

    public string MediaType { get; set; } = "image"; // image or video

    public int SortOrder { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Review Review { get; set; } = null!;
}
