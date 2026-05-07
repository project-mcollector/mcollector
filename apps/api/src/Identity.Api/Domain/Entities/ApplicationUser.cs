using Microsoft.AspNetCore.Identity;

namespace Identity.Api.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    // Navigation property for projects this user has access to
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}
