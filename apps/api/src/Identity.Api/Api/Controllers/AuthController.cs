using Identity.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Protocols.Configuration;

namespace Identity.Api.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController(UserManager<ApplicationUser> userManager, IConfiguration configuration) : ControllerBase
{
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized("Invalid credentials");

        var jwtKey = configuration["Jwt:Key"] ??
                        throw new InvalidConfigurationException("Provide jwt key in configuration");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: new(key, SecurityAlgorithms.HmacSha256)
        );

        return Ok(new LoginResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresIn = TimeSpan.FromDays(7).TotalSeconds
        });
    }
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public double ExpiresIn { get; set; }
}
