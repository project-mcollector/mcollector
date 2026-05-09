using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Identity.Api.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
