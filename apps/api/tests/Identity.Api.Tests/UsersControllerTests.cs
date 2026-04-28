using System.Security.Claims;
using Identity.Api.Api.Controllers;
using Identity.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Identity.Api.Tests;

public class UsersControllerTests
{
    [Fact]
    public async Task GetCurrentUser_ReturnsProfileWithProjects()
    {
        await using var dbContext = TestHelpers.CreateDbContext();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = "user@acme.dev",
            UserName = "user@acme.dev"
        };
        var project = new Project
        {
            Name = "Platform",
            Description = "Core platform"
        };

        user.Projects.Add(project);
        dbContext.Users.Add(user);
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();

        var userManager = TestHelpers.CreateUserManagerMock();
        userManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

        var controller = new UsersController(userManager.Object, dbContext)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new(new ClaimsIdentity([
                        new Claim(ClaimTypes.NameIdentifier, user.Id)
                    ], "TestAuth"))
                }
            }
        };

        var result = await controller.GetCurrentUser();

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<UserProfileResponse>(ok.Value);
        Assert.Equal(user.Id, response.Id);
        Assert.Equal(user.Email, response.Email);
        Assert.Equal(user.UserName, response.UserName);
        Assert.Single(response.Projects);
        Assert.Equal(project.Id, response.Projects[0].Id);
        Assert.Equal(project.Name, response.Projects[0].Name);
    }

    [Fact]
    public async Task GetCurrentUser_UserNotFound_ReturnsNotFound()
    {
        await using var dbContext = TestHelpers.CreateDbContext();
        var userManager = TestHelpers.CreateUserManagerMock();
        userManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((ApplicationUser?)null);

        var controller = new UsersController(userManager.Object, dbContext)
        {
            ControllerContext = new()
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new(new ClaimsIdentity("TestAuth"))
                }
            }
        };

        var result = await controller.GetCurrentUser();

        Assert.IsType<NotFoundResult>(result);
    }
}
