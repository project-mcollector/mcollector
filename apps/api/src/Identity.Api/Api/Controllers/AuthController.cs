using Identity.Api.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthTokenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await authService.LoginAsync(request.Email, request.Password);
        return result.IsSuccess ? Ok(result.Value) : Unauthorized("Invalid credentials");
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthTokenDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await authService.RegisterAsync(request.Email, request.Password);
        return result.IsSuccess ? Created("/api/users/me", result.Value) : BadRequest(result.Error?.Description);
    }
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? OrganizationName { get; set; }
}
