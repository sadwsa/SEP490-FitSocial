using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class Role
{
    public string RoleCode { get; set; } = null!;

    public string RoleName { get; set; } = null!;

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
