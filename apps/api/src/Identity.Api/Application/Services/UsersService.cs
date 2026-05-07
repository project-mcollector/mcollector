using Identity.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Utilities;

namespace Identity.Api.Application.Services;

public record UserProfileDto(string Id, string? Email, string? UserName, List<UserProjectDto> Projects);

public record UserProjectDto(Guid Id, string Name, string Description);

public interface IUsersService
{
    Task<Result<UserProfileDto>> GetCurrentUserAsync(string userId);
}

public class UsersService(IdentityAppDbContext dbContext) : IUsersService
{
    public async Task<Result<UserProfileDto>> GetCurrentUserAsync(string userId)
    {
        var user = await dbContext.Users
            .Include(u => u.Projects)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null) return Errors.NotFound("User", userId);

        return new UserProfileDto(
            user.Id,
            user.Email,
            user.UserName,
            user.Projects.Select(p => new UserProjectDto(p.Id, p.Name, p.Description)).ToList()
        );
    }
}
