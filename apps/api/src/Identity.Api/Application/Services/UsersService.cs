using Identity.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Utilities;
using Identity.Api.Domain.Entities;

namespace Identity.Api.Application.Services;

public record UserProfileDto(string Id, string? Email, string? UserName, List<UserProjectDto> Projects);

public record UserProjectDto(Guid Id, string Name, string Description);

public interface IUsersService
{
    Task<Result<UserProfileDto>> GetCurrentUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAccountAsync(string userId, CancellationToken cancellationToken = default);
}

public class UsersService(IdentityAppDbContext dbContext, UserManager<ApplicationUser> userManager) : IUsersService
{
    public async Task<Result<UserProfileDto>> GetCurrentUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .Include(u => u.Projects)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null) return Errors.NotFound("User", userId);

        return new UserProfileDto(
            user.Id,
            user.Email,
            user.UserName,
            user.Projects.Select(p => new UserProjectDto(p.Id, p.Name, p.Description)).ToList()
        );
    }

    public async Task<Result> DeleteAccountAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .Include(u => u.Projects)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null) return Errors.NotFound("User", userId);

        // Projects are not cascade-deleted when user is deleted, so remove them explicitly
        dbContext.Projects.RemoveRange(user.Projects);
        await dbContext.SaveChangesAsync(cancellationToken);

        var result = await userManager.DeleteAsync(user);
        return result.Succeeded
            ? Result.Success()
            : Errors.Internal(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
