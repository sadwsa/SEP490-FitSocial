using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class Post
{
    public Guid Id { get; set; }

    public Guid AuthorId { get; set; }

    public string? Content { get; set; }

    public Guid? SportId { get; set; }

    public Guid? LocationId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool? IsDeleted { get; set; }

    public virtual User Author { get; set; } = null!;

    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public virtual Location? Location { get; set; }

    public virtual ICollection<PostInteraction> PostInteractions { get; set; } = new List<PostInteraction>();

    public virtual ICollection<PostMedium> PostMedia { get; set; } = new List<PostMedium>();

    public virtual ICollection<Report> Reports { get; set; } = new List<Report>();

    public virtual Sport? Sport { get; set; }
}
