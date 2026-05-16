using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Identity.Api.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    protected string? UserEmail => User.FindFirstValue(ClaimTypes.Email);
    protected string RequiredUserId => UserId ?? throw new InvalidOperationException("UserId missing on authorized endpoint");
}
