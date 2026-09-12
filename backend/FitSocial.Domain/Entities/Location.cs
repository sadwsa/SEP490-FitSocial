using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class Location
{
    public Guid LocationId { get; set; }

    public string LocationName { get; set; } = null!;

    public string? Address { get; set; }

    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();

    public virtual ICollection<CoachProfile> Coaches { get; set; } = new List<CoachProfile>();
}
