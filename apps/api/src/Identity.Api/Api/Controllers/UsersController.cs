using Identity.Api.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Identity.Api.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController(IUsersService usersService) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var result = await usersService.GetCurrentUserAsync(userId);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }
}
