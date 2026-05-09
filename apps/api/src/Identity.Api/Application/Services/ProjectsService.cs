using Identity.Api.Domain.Entities;
using Identity.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Utilities;

namespace Identity.Api.Application.Services;

public record ProjectDto(Guid Id, string Name, string Description, string ApiKey);

public record ProjectWithMembersDto(
    Guid Id,
    string Name,
    string Description,
    string ApiKey,
    List<ProjectMemberDto> Members);

public record ProjectMemberDto(string Id, string Email);

public interface IProjectsService
{
    Task<Result<List<ProjectDto>>> GetProjectsAsync(string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectDto>> CreateProjectAsync(string userId, string name, string description, CancellationToken cancellationToken = default);
    Task<Result<ProjectWithMembersDto>> GetProjectAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectDto>> UpdateProjectAsync(Guid id, string userId, string name, string? description, CancellationToken cancellationToken = default);
    Task<Result> DeleteProjectAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectDto>> RegenerateApiKeyAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<Result> AddMemberAsync(Guid id, string userId, string memberEmail, CancellationToken cancellationToken = default);
    Task<Result> RemoveMemberAsync(Guid id, string userId, string memberId, CancellationToken cancellationToken = default);
}

public class ProjectsService(
    IdentityAppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IApiKeyService apiKeyService,
    ILogger<ProjectsService> logger) : IProjectsService
{
    public async Task<Result<List<ProjectDto>>> GetProjectsAsync(string userId, CancellationToken cancellationToken = default)
        => await dbContext.Projects
            .Where(p => p.Users.Any(u => u.Id == userId))
            .Select(p => new ProjectDto(p.Id, p.Name, p.Description, p.ApiKey))
            .ToListAsync(cancellationToken);

    public async Task<Result<ProjectDto>> CreateProjectAsync(string userId, string name, string description, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.FindAsync([userId], cancellationToken);
        if (user is null) return Errors.NotFound("User", userId);

        var project = new Project
        {
            Name = name,
            Description = description,
            ApiKey = apiKeyService.GenerateApiKey()
        };

        project.Users.Add(user);
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Project {ProjectId} '{Name}' created by user {UserId}", project.Id, project.Name, userId);
        return new ProjectDto(project.Id, project.Name, project.Description, project.ApiKey);
    }

    public async Task<Result<ProjectWithMembersDto>> GetProjectAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var project = await FindProjectAsync(id, userId, includeUsers: true, cancellationToken);
        if (project is null) return Errors.NotFound("Project", id);
        return ToProjectWithMembersDto(project);
    }

    public async Task<Result<ProjectDto>> UpdateProjectAsync(Guid id, string userId, string name, string? description, CancellationToken cancellationToken = default)
    {
        var project = await FindProjectAsync(id, userId, cancellationToken: cancellationToken);
        if (project is null) return Errors.NotFound("Project", id);

        project.Name = name;
        if (description is not null)
            project.Description = description;
        await dbContext.SaveChangesAsync(cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Project {ProjectId} updated by user {UserId}", id, userId);
        return new ProjectDto(project.Id, project.Name, project.Description, project.ApiKey);
    }

    public async Task<Result> DeleteProjectAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var project = await FindProjectAsync(id, userId, cancellationToken: cancellationToken);
        if (project is null) return Errors.NotFound("Project", id);

        dbContext.Projects.Remove(project);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Project {ProjectId} deleted by user {UserId}", id, userId);
        return Result.Success();
    }

    public async Task<Result<ProjectDto>> RegenerateApiKeyAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var project = await FindProjectAsync(id, userId, cancellationToken: cancellationToken);
        if (project is null) return Errors.NotFound("Project", id);

        project.ApiKey = apiKeyService.GenerateApiKey();
        await dbContext.SaveChangesAsync(cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("API key regenerated for project {ProjectId} by user {UserId}", id, userId);
        return new ProjectDto(project.Id, project.Name, project.Description, project.ApiKey);
    }

    public async Task<Result> AddMemberAsync(Guid id, string userId, string memberEmail, CancellationToken cancellationToken = default)
    {
        var project = await FindProjectAsync(id, userId, includeUsers: true, cancellationToken);
        if (project is null) return Errors.NotFound("Project", id);

        var userToAdd = await userManager.FindByEmailAsync(memberEmail);
        if (userToAdd is null)
        {
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("AddMember: no user found with email {MemberEmail} for project {ProjectId}", memberEmail, id);
            return Errors.Validation("Email", "User not found");
        }

        if (project.Users.Any(u => u.Id == userToAdd.Id))
        {
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("AddMember: user {MemberId} is already a member of project {ProjectId}", userToAdd.Id, id);
            return Errors.Conflict("User is already a member of this project");
        }

        project.Users.Add(userToAdd);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("User {MemberId} ({MemberEmail}) added to project {ProjectId} by {UserId}",
                userToAdd.Id, memberEmail, id, userId);
        return Result.Success();
    }

    public async Task<Result> RemoveMemberAsync(Guid id, string userId, string memberId, CancellationToken cancellationToken = default)
    {
        var project = await FindProjectAsync(id, userId, includeUsers: true, cancellationToken);
        if (project is null) return Errors.NotFound("Project", id);

        var userToRemove = project.Users.FirstOrDefault(u => u.Id == memberId);
        if (userToRemove is null)
            return Errors.NotFound("Member", memberId);

        project.Users.Remove(userToRemove);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Member {MemberId} removed from project {ProjectId} by user {UserId}", memberId, id, userId);
        return Result.Success();
    }

    private Task<Project?> FindProjectAsync(Guid projectId, string userId, bool includeUsers = false, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Projects.AsQueryable();
        if (includeUsers)
            query = query.Include(p => p.Users);
        return query.FirstOrDefaultAsync(p => p.Id == projectId && p.Users.Any(u => u.Id == userId), cancellationToken);
    }

    private static ProjectWithMembersDto ToProjectWithMembersDto(Project project) =>
        new(project.Id, project.Name, project.Description, project.ApiKey,
            project.Users.Select(u => new ProjectMemberDto(u.Id, u.Email ?? string.Empty)).ToList());
}
