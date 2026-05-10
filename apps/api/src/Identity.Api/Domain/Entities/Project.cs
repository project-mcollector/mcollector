namespace Identity.Api.Domain.Entities;

public class Project
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    // Navigation property for users that have access to this project
    public virtual ICollection<ApplicationUser> Users { get; init; } = [];
}
