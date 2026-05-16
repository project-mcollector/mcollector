using Identity.Api.Api.Requests;
using Identity.Api.Application.Services;
using Identity.Api.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Identity.Api.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService, UserManager<ApplicationUser> userManager) : ApiControllerBase
{
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthTokenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request.Email, request.Password, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : result.Error.Id == "Email.NotConfirmed"
                ? Forbid()
                : Unauthorized();
    }

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request.Email, request.Password, cancellationToken);
        return result.IsSuccess ? Created() : BadRequest(result.Error.Description);
    }

    [HttpPost("passkey/options")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPasskeyOptions([FromBody] PasskeyOptionsRequest request)
    {
        var result = string.IsNullOrEmpty(request.Email)
            ? await authService.BeginDiscoverablePasskeyLoginAsync()
            : await authService.BeginPasskeyLoginAsync(request.Email);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error.Description);
    }

    [HttpPost("passkey/login")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PasskeyLogin([FromBody] PasskeyCredentialRequest request)
    {
        var result = await authService.CompletePasskeyLoginAsync(request.CredentialJson);
        return result.IsSuccess ? Ok(result.Value) : Unauthorized(result.Error.Description);
    }

    [Authorize]
    [HttpGet("passkey")]
    [EnableRateLimiting("api")]
    [ProducesResponseType(typeof(IReadOnlyList<PasskeyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPasskeys()
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return Unauthorized();

        var result = await authService.GetPasskeysAsync(user);
        return result.IsSuccess
            ? Ok(result.Value)
            : StatusCode(StatusCodes.Status500InternalServerError, result.Error.Description);
    }

    [Authorize]
    [HttpDelete("passkey/{credentialId}")]
    [EnableRateLimiting("api")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePasskey(string credentialId)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return Unauthorized();

        var result = await authService.DeletePasskeyAsync(user, credentialId);
        return result.IsSuccess ? NoContent() :
            result.Error.Id.EndsWith(".NotFound") ? NotFound(result.Error.Description) :
            BadRequest(result.Error.Description);
    }

    [Authorize]
    [HttpPost("passkey/register/options")]
    [EnableRateLimiting("api")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPasskeyRegistrationOptions()
    {
        // Passkey creation supported for existing accounts only
        var user = await GetCurrentUserAsync();
        if (user is null) return Unauthorized();

        var result = await authService.BeginPasskeyRegistrationAsync(user);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error.Description);
    }

    [Authorize]
    [HttpPost("passkey/register")]
    [EnableRateLimiting("api")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RegisterPasskey([FromBody] PasskeyCredentialRequest request)
    {
        // Passkey creation supported for existing accounts only
        var user = await GetCurrentUserAsync();
        if (user is null) return Unauthorized();

        var result = await authService.CompletePasskeyRegistrationAsync(user, request.CredentialJson);
        return result.IsSuccess ? Ok() : BadRequest(result.Error.Description);
    }

    [HttpPost("confirm-email")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthTokenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.ConfirmEmail(request.UserId, request.Token, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error.Description);
    }

    [HttpPost("resend-confirmation")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResendConfirmation([FromBody] ResendConfirmationRequest request,
        CancellationToken cancellationToken)
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
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await authService.ForgotPasswordAsync(request.Email, cancellationToken);
        return NoContent();
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.ResetPasswordAsync(request.UserId, request.Token, request.Password, cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error.Description);
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
        await authService.RevokeOtherSessionsAsync(request.RefreshToken, RequiredUserId, cancellationToken);
        return NoContent();
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        if (UserId is null) return null;
        return await userManager.FindByIdAsync(UserId);
    }
}
