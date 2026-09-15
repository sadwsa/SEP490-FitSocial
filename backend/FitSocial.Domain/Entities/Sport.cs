using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class Sport
{
    public Guid SportId { get; set; }

    public string SportName { get; set; } = null!;

    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();

    public virtual ICollection<CoachProfile> Coaches { get; set; } = new List<CoachProfile>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
