using Identity.Api.Domain.Entities;
using Identity.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Utilities;

namespace Identity.Api.Application.Services;

public record ProjectDto(Guid Id, string Name, string Description, string ApiKey);
public record ProjectWithMembersDto(Guid Id, string Name, string Description, string ApiKey, List<ProjectMemberDto> Members);
public record ProjectMemberDto(string Id, string Email);

public interface IProjectsService
{
    Task<Result<List<ProjectDto>>> GetProjectsAsync(string userId);
    Task<Result<ProjectDto>> CreateProjectAsync(string userId, string name, string description);
    Task<Result<ProjectWithMembersDto>> GetProjectAsync(Guid id, string userId);
    Task<Result<ProjectDto>> UpdateProjectAsync(Guid id, string userId, string name, string description);
    Task<Result> DeleteProjectAsync(Guid id, string userId);
    Task<Result<ProjectDto>> RegenerateApiKeyAsync(Guid id, string userId);
    Task<Result> AddMemberAsync(Guid id, string userId, string memberEmail);
    Task<Result> RemoveMemberAsync(Guid id, string userId, string memberId);
}

public class ProjectsService(
    IdentityAppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IApiKeyService apiKeyService,
    ILogger<ProjectsService> logger) : IProjectsService
{
    public async Task<Result<List<ProjectDto>>> GetProjectsAsync(string userId)
    {
        var projects = await dbContext.Projects
            .Where(p => p.Users.Any(u => u.Id == userId))
            .Select(p => new ProjectDto(p.Id, p.Name, p.Description, p.ApiKey))
            .ToListAsync();

        return projects;
    }

    public async Task<Result<ProjectDto>> CreateProjectAsync(string userId, string name, string description)
    {
        var user = await dbContext.Users.FindAsync(userId);
        if (user is null) return Errors.Unauthorized(userId);

        var project = new Project
        {
            Name = name,
            Description = description,
            ApiKey = apiKeyService.GenerateApiKey()
        };

        project.Users.Add(user);
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Project {ProjectId} '{Name}' created by user {UserId}", project.Id, project.Name, userId);
        return new ProjectDto(project.Id, project.Name, project.Description, project.ApiKey);
    }

    public async Task<Result<ProjectWithMembersDto>> GetProjectAsync(Guid id, string userId)
    {
        var project = await FindProjectAsync(id, userId);
        if (project is null) return Errors.NotFound("Project", id);
        return ToWithMembersDto(project);
    }

    public async Task<Result<ProjectDto>> UpdateProjectAsync(Guid id, string userId, string name, string description)
    {
        var project = await FindProjectAsync(id, userId);
        if (project is null) return Errors.NotFound("Project", id);

        project.Name = name;
        project.Description = description;
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Project {ProjectId} updated by user {UserId}", id, userId);
        return new ProjectDto(project.Id, project.Name, project.Description, project.ApiKey);
    }

    public async Task<Result> DeleteProjectAsync(Guid id, string userId)
    {
        var project = await FindProjectAsync(id, userId);
        if (project is null) return Errors.NotFound("Project", id);

        dbContext.Projects.Remove(project);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Project {ProjectId} deleted by user {UserId}", id, userId);
        return Result.Success();
    }

    public async Task<Result<ProjectDto>> RegenerateApiKeyAsync(Guid id, string userId)
    {
        var project = await FindProjectAsync(id, userId);
        if (project is null) return Errors.NotFound("Project", id);

        project.ApiKey = apiKeyService.GenerateApiKey();
        await dbContext.SaveChangesAsync();

        logger.LogInformation("API key regenerated for project {ProjectId} by user {UserId}", id, userId);
        return new ProjectDto(project.Id, project.Name, project.Description, project.ApiKey);
    }

    public async Task<Result> AddMemberAsync(Guid id, string userId, string memberEmail)
    {
        var project = await FindProjectAsync(id, userId);
        if (project is null) return Errors.NotFound("Project", id);

        var userToAdd = await userManager.FindByEmailAsync(memberEmail);
        if (userToAdd is null)
        {
            logger.LogWarning("AddMember: no user found with email {MemberEmail} for project {ProjectId}", memberEmail, id);
            return Errors.Validation("Email", $"No user found with email '{memberEmail}'");
        }

        if (project.Users.Any(u => u.Id == userToAdd.Id))
        {
            logger.LogWarning("AddMember: user {MemberId} is already a member of project {ProjectId}", userToAdd.Id, id);
            return Errors.Conflict("User is already a member of this project");
        }

        project.Users.Add(userToAdd);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("User {MemberId} ({MemberEmail}) added to project {ProjectId} by {UserId}", userToAdd.Id, memberEmail, id, userId);
        return Result.Success();
    }

    public async Task<Result> RemoveMemberAsync(Guid id, string userId, string memberId)
    {
        var project = await FindProjectAsync(id, userId);
        if (project is null) return Errors.NotFound("Project", id);

        var userToRemove = project.Users.FirstOrDefault(u => u.Id == memberId);
        if (userToRemove is null)
            return Errors.NotFound("Member", memberId);

        project.Users.Remove(userToRemove);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Member {MemberId} removed from project {ProjectId} by user {UserId}", memberId, id, userId);
        return Result.Success();
    }

    private Task<Project?> FindProjectAsync(Guid projectId, string userId) =>
        dbContext.Projects
            .Include(p => p.Users)
            .FirstOrDefaultAsync(p => p.Id == projectId && p.Users.Any(u => u.Id == userId));

    private static ProjectWithMembersDto ToWithMembersDto(Project project) =>
        new(project.Id, project.Name, project.Description, project.ApiKey,
            project.Users.Select(u => new ProjectMemberDto(u.Id, u.Email ?? string.Empty)).ToList());
}
