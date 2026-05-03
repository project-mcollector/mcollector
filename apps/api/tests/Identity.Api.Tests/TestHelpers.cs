using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Tests;

internal static class TestHelpers
{
    internal static ControllerContext ControllerContextFor(string userId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId)], "TestAuth"))
        }
    };
}
