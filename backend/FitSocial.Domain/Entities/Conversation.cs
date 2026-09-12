using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class Conversation
{
    public Guid Id { get; set; }

    public Guid? CreatorId { get; set; }

    public string? Type { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool? IsDeleted { get; set; }

    public virtual User? Creator { get; set; }

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    public virtual ICollection<Participant> Participants { get; set; } = new List<Participant>();
}
