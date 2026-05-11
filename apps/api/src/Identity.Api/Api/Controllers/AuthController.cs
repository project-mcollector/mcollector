using Identity.Api.Api.Requests;
using Identity.Api.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Identity.Api.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService) : ApiControllerBase
{
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthTokenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request.Email, request.Password, cancellationToken);
        if (result.IsSuccess) return Ok(result.Value);
        if (result.Error?.Id == "Email.NotConfirmed") return StatusCode(403);
        return Unauthorized();
    }

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request.Email, request.Password, cancellationToken);
        return result.IsSuccess ? Created() : BadRequest(result.Error?.Description);
    }

    [HttpPost("confirm-email")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthTokenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.ConfirmEmail(request.UserId, request.Token, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error?.Description);
    }

    [HttpPost("resend-confirmation")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResendConfirmation([FromBody] ResendConfirmationRequest request, CancellationToken cancellationToken)
    {
        await authService.ResendConfirmationEmailAsync(request.Email, cancellationToken);
        return NoContent();
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthTokenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RefreshAsync(request.RefreshToken, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Unauthorized("Invalid or expired refresh token");
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await authService.ForgotPasswordAsync(request.Email, cancellationToken);
        return NoContent();
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.ResetPasswordAsync(request.UserId, request.Token, request.Password, cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error?.Description);
    }

    [HttpPost("logout")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RevokeRequest request, CancellationToken cancellationToken)
    {
        await authService.RevokeAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }

    [HttpPost("logout-other")]
    [Authorize]
    [EnableRateLimiting("api")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LogoutOther([FromBody] RevokeRequest request, CancellationToken cancellationToken)
    {
        if (UserId is null)
            return Unauthorized();

        await authService.RevokeOtherSessionsAsync(request.RefreshToken, UserId, cancellationToken);
        return NoContent();
    }
}
