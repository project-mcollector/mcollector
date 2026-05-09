using Identity.Api.Api.Controllers;
using Identity.Api.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Utilities;

namespace Identity.Api.Tests;

public class ProjectsControllerTests
{
    private static readonly string UserId = Guid.NewGuid().ToString();

    [Fact]
    public async Task GetProjects_ReturnsOkWithList()
    {
        var projects = new List<ProjectDto>
        {
            new(Guid.NewGuid(), "Alpha", "desc", "proj_abc"),
            new(Guid.NewGuid(), "Beta", "desc", "proj_xyz"),
        };

        var service = new Mock<IProjectsService>();
        service.Setup(x => x.GetProjectsAsync(UserId, default)).ReturnsAsync(projects);

        var controller = new ProjectsController(service.Object)
        {
            ControllerContext = TestHelpers.ControllerContextFor(UserId)
        };

        var result = await controller.GetProjects(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(projects, ok.Value);
    }

    [Fact]
    public async Task GetProject_NotFound_Returns404()
    {
        var id = Guid.NewGuid();
        var service = new Mock<IProjectsService>();
        service.Setup(x => x.GetProjectAsync(id, UserId, default))
            .ReturnsAsync(Errors.NotFound("Project", id));

        var controller = new ProjectsController(service.Object)
        {
            ControllerContext = TestHelpers.ControllerContextFor(UserId)
        };

        var result = await controller.GetProject(id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteProject_Success_Returns204()
    {
        var id = Guid.NewGuid();
        var service = new Mock<IProjectsService>();
        service.Setup(x => x.DeleteProjectAsync(id, UserId, default)).ReturnsAsync(Result.Success());

        var controller = new ProjectsController(service.Object)
        {
            ControllerContext = TestHelpers.ControllerContextFor(UserId)
        };

        var result = await controller.DeleteProject(id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task AddMember_AlreadyMember_Returns400()
    {
        var id = Guid.NewGuid();
        var service = new Mock<IProjectsService>();
        service.Setup(x => x.AddMemberAsync(id, UserId, "dup@example.com", default))
            .ReturnsAsync(Errors.Conflict("User is already a member of this project"));

        var controller = new ProjectsController(service.Object)
        {
            ControllerContext = TestHelpers.ControllerContextFor(UserId)
        };

        var result = await controller.AddMember(id, new() { Email = "dup@example.com" }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
