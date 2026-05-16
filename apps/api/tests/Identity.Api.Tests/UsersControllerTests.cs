using Identity.Api.Api.Controllers;
using Identity.Api.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Utilities;

namespace Identity.Api.Tests;

public class UsersControllerTests
{
    private static readonly string UserId = Guid.NewGuid().ToString();

    private static UsersController CreateController(Mock<IUsersService> service)
        => new(service.Object) { ControllerContext = TestHelpers.ControllerContextFor(UserId) };

    // GetCurrentUser

    [Fact]
    public async Task GetCurrentUser_ReturnsProfileWithProjects()
    {
        var profile = new UserProfileDto(
            UserId, "user@acme.dev", "user@acme.dev",
            [new(Guid.NewGuid(), "Platform", "Core platform")]);

        var service = new Mock<IUsersService>();
        service.Setup(x => x.GetCurrentUserAsync(UserId, default)).ReturnsAsync(profile);

        var controller = CreateController(service);
        var result = await controller.GetCurrentUser(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<UserProfileDto>(ok.Value);
        Assert.Equal(UserId, response.Id);
        Assert.Equal("user@acme.dev", response.Email);
        Assert.Single(response.Projects);
    }

    [Fact]
    public async Task GetCurrentUser_UserNotFound_ReturnsNotFound()
    {
        var service = new Mock<IUsersService>();
        service.Setup(x => x.GetCurrentUserAsync(UserId, default))
            .ReturnsAsync(Errors.NotFound("User", UserId));

        var controller = CreateController(service);
        var result = await controller.GetCurrentUser(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetCurrentUser_NoProjects_ReturnsEmptyProjectsList()
    {
        var profile = new UserProfileDto(UserId, "user@acme.dev", "user@acme.dev", []);

        var service = new Mock<IUsersService>();
        service.Setup(x => x.GetCurrentUserAsync(UserId, default)).ReturnsAsync(profile);

        var controller = CreateController(service);
        var result = await controller.GetCurrentUser(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<UserProfileDto>(ok.Value);
        Assert.Empty(response.Projects);
    }

    // DeleteAccount

    [Fact]
    public async Task DeleteAccount_Success_ReturnsNoContent()
    {
        var service = new Mock<IUsersService>();
        service.Setup(x => x.DeleteAccountAsync(UserId, default))
            .ReturnsAsync(Result.Success());

        var controller = CreateController(service);
        var result = await controller.DeleteAccount(CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteAccount_UserNotFound_ReturnsNotFound()
    {
        var service = new Mock<IUsersService>();
        service.Setup(x => x.DeleteAccountAsync(UserId, default))
            .ReturnsAsync(Errors.NotFound("User", UserId));

        var controller = CreateController(service);
        var result = await controller.DeleteAccount(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
