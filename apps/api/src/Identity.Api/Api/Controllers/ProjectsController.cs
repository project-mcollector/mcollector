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
public class ProjectsController(UserManager<ApplicationUser> userManager, IdentityAppDbContext dbContext)
    : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var dbUser = await dbContext.Users.FindAsync(user.Id);

        var project = new Project
        {
            Name = request.Name,
            Description = request.Description ?? string.Empty
        };

        project.Users.Add(dbUser ?? throw new InvalidOperationException("User not found"));
        dbContext.Projects.Add(project);

        await dbContext.SaveChangesAsync();

        return Ok(new ProjectResponse { Id = project.Id, Name = project.Name, Description = project.Description });
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProjectWithMembersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetProject(Guid id)
        => ExecuteWithProjectAsync(id, async project
            => Ok(new ProjectWithMembersResponse
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                Members = project.Users.Select(u => new ProjectMemberDto { Id = u.Id, Email = u.Email ?? string.Empty })
                    .ToList()
            }));

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> UpdateProject(Guid id, [FromBody] CreateProjectRequest request)
        => ExecuteWithProjectAsync(id, async project =>
        {
            project.Name = request.Name;
            project.Description = request.Description ?? string.Empty;

            await dbContext.SaveChangesAsync();

            return Ok(new ProjectResponse { Id = project.Id, Name = project.Name, Description = project.Description });
        });

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> DeleteProject(Guid id)
        => ExecuteWithProjectAsync(id, async project =>
        {
            dbContext.Projects.Remove(project);
            await dbContext.SaveChangesAsync();

            return NoContent();
        });

    [HttpPost("{id:guid}/members")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> AddMember(Guid id, [FromBody] AddMemberRequest request)
        => ExecuteWithProjectAsync(id, async project =>
        {
            var userToAdd = await userManager.FindByEmailAsync(request.Email);
            if (userToAdd is null) return BadRequest("User not found");

            if (project.Users.Any(u => u.Id == userToAdd.Id))
                return BadRequest("User is already a member");

            project.Users.Add(userToAdd);
            await dbContext.SaveChangesAsync();

            return Ok();
        });

    [HttpDelete("{id:guid}/members/{userId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> RemoveMember(Guid id, string userId)
        => ExecuteWithProjectAsync(id, async project =>
        {
            var userToRemove = project.Users.FirstOrDefault(u => u.Id == userId);
            if (userToRemove is null) return NotFound("Member not found in project");

            project.Users.Remove(userToRemove);
            await dbContext.SaveChangesAsync();

            return NoContent();
        });


    /// <summary>
    /// Executes action with a project context verifying existence and user's access
    /// </summary>
    /// <param name="projectId">Id of the project</param>
    /// <param name="action">The delegate action to execute if the project is found and accessible</param>
    /// <returns>The result of the action, or an error response if validation fails</returns>
    private async Task<IActionResult> ExecuteWithProjectAsync(Guid projectId, Func<Project, Task<IActionResult>> action)
    {
        var (_, project, error) = await GetUserAndProjectAsync(projectId);
        if (error is not null || project is null) return error ?? NotFound();

        return await action(project);
    }

    /// <summary>
    /// Retrieves user and the specified project, verifying user's access
    /// </summary>
    /// <param name="projectId">Id of the project</param>
    /// <returns>A tuple containing the authenticated user, requested project, or an error result if validation fails</returns>
    private async Task<(ApplicationUser? User, Project? project, IActionResult? error)> GetUserAndProjectAsync(
        Guid projectId)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return new(null, null, Unauthorized());

        var project = await dbContext.Projects
            .Include(p => p.Users)
            .FirstOrDefaultAsync(p => p.Id == projectId && p.Users.Any(u => u.Id == user.Id));

        if (project is null) return (user, null, NotFound());

        return (user, project, null);
    }
}

public class AddMemberRequest
{
    public string Email { get; set; } = string.Empty;
}

public class CreateProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class ProjectResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class ProjectWithMembersResponse : ProjectResponse
{
    public List<ProjectMemberDto> Members { get; set; } = new();
}

public class ProjectMemberDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
