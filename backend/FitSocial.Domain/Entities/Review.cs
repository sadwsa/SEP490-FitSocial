using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class Review
{
    public Guid ReviewId { get; set; }

    public Guid TraineeId { get; set; }

    public Guid CoachId { get; set; }

    public int? Rating { get; set; }

    public string? Comment { get; set; }

    public string? Reply { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual CoachProfile Coach { get; set; } = null!;

    public virtual User Trainee { get; set; } = null!;
}
