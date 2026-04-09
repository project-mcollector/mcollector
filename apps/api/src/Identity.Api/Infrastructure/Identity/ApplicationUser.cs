using Microsoft.AspNetCore.Identity;

namespace Identity.Api.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    // Navigation property for projects this user has access to
    public virtual ICollection<Project> Projects { get; set; } = new List<Project>();
}
