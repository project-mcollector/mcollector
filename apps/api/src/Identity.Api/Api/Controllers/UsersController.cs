using Identity.Api.Infrastructure.Identity;
using Identity.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Api.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class UsersController(UserManager<ApplicationUser> userManager, IdentityAppDbContext dbContext)
    : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        var userWithProjects = await dbContext.Users
            .Include(u => u.Projects)
            .FirstOrDefaultAsync(u => u.Id == user.Id);

        return Ok(new UserProfileResponse
        {
            Id = userWithProjects?.Id ?? string.Empty,
            Email = userWithProjects?.Email,
            UserName = userWithProjects?.UserName,
            Projects = userWithProjects?.Projects.Select(p => new UserProjectDto { Id = p.Id, Name = p.Name, Description = p.Description }).ToList() ?? new List<UserProjectDto>()
        });
    }
}

public class UserProfileResponse
{
    public string Id { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? UserName { get; set; }
    public List<UserProjectDto> Projects { get; set; } = new();
}

public class UserProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
