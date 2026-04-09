using System.Collections.Generic;

namespace Identity.Api.Infrastructure.Identity;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Navigation property for users that have access to this project
    public virtual ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
}

