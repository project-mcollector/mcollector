using Identity.Api.Api.Requests;
using Identity.Api.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Utilities;

namespace Identity.Api.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableRateLimiting("api")]
public class ProjectsController(IProjectsService projectsService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<ProjectDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProjects(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UserId)) return Unauthorized();
        var result = await projectsService.GetProjectsAsync(UserId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapError(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UserId)) return Unauthorized();
        var result = await projectsService.CreateProjectAsync(UserId, request.Name, request.Description ?? string.Empty,
            cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetProject), new { id = result.Value?.Id }, result.Value)
            : MapError(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProjectWithMembersDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProject(Guid id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UserId)) return Unauthorized();
        var result = await projectsService.GetProjectAsync(id, UserId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapError(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProject(Guid id, [FromBody] CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UserId)) return Unauthorized();
        var result =
            await projectsService.UpdateProjectAsync(id, UserId, request.Name, request.Description, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapError(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProject(Guid id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UserId)) return Unauthorized();
        var result = await projectsService.DeleteProjectAsync(id, UserId, cancellationToken);
        return result.IsSuccess ? NoContent() : MapError(result);
    }

    [HttpPost("{id:guid}/api-key/regenerate")]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegenerateApiKey(Guid id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UserId)) return Unauthorized();
        var result = await projectsService.RegenerateApiKeyAsync(id, UserId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapError(result);
    }

    [HttpPost("{id:guid}/members")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddMemberRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UserId)) return Unauthorized();
        var result = await projectsService.AddMemberAsync(id, UserId, request.Email, cancellationToken);
        return result.IsSuccess ? Ok() : MapError(result);
    }

    [HttpDelete("{id:guid}/members/{memberId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(Guid id, string memberId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UserId)) return Unauthorized();
        var result = await projectsService.RemoveMemberAsync(id, UserId, memberId, cancellationToken);
        return result.IsSuccess ? NoContent() : MapError(result);
    }

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
