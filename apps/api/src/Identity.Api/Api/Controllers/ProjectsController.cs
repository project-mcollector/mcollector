using Identity.Api.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Utilities;

namespace Identity.Api.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProjectsController(IProjectsService projectsService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<ProjectDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProjects()
    {
        var result = await projectsService.GetProjectsAsync(UserId);
        return result.IsSuccess ? Ok(result.Value) : MapError(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest request)
    {
        var result =
            await projectsService.CreateProjectAsync(UserId, request.Name, request.Description ?? string.Empty);
        return result.IsSuccess ? Ok(result.Value) : MapError(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProjectWithMembersDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProject(Guid id)
    {
        var result = await projectsService.GetProjectAsync(id, UserId);
        return result.IsSuccess ? Ok(result.Value) : MapError(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProject(Guid id, [FromBody] CreateProjectRequest request)
    {
        var result =
            await projectsService.UpdateProjectAsync(id, UserId, request.Name, request.Description ?? string.Empty);
        return result.IsSuccess ? Ok(result.Value) : MapError(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProject(Guid id)
    {
        var result = await projectsService.DeleteProjectAsync(id, UserId);
        return result.IsSuccess ? NoContent() : MapError(result);
    }

    [HttpPost("{id:guid}/api-key/regenerate")]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegenerateApiKey(Guid id)
    {
        var result = await projectsService.RegenerateApiKeyAsync(id, UserId);
        return result.IsSuccess ? Ok(result.Value) : MapError(result);
    }

    [HttpPost("{id:guid}/members")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddMemberRequest request)
    {
        var result = await projectsService.AddMemberAsync(id, UserId, request.Email);
        return result.IsSuccess ? Ok() : MapError(result);
    }

    [HttpDelete("{id:guid}/members/{memberId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(Guid id, string memberId)
    {
        var result = await projectsService.RemoveMemberAsync(id, UserId, memberId);
        return result.IsSuccess ? NoContent() : MapError(result);
    }

    private string UserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();

    private IActionResult MapError(Result result)
    {
        var error = result.Error ?? throw new InvalidOperationException("MapError called on a successful result");
        return error.Id switch
        {
            var id when id.EndsWith("NotFound") => NotFound(),
            "Unauthorized" => Unauthorized(),
            "Conflict" => BadRequest(error.Description),
            var id when id.StartsWith("Validation") => BadRequest(error.Description),
            _ => StatusCode(500, error.Description)
        };
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
