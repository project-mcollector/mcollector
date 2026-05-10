using Identity.Api.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Identity.Api.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableRateLimiting("api")]
public class UsersController(IUsersService usersService) : ApiControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var result = await usersService.GetCurrentUserAsync(UserId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpDelete("me")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAccount(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var result = await usersService.DeleteAccountAsync(UserId, cancellationToken);
        return result.IsSuccess ? NoContent() : NotFound();
    }
}
