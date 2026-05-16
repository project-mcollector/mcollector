using Identity.Api.Api.Controllers;
using Identity.Api.Api.Requests;
using Identity.Api.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Utilities;

namespace Identity.Api.Tests;

public class ProjectsControllerTests
{
    private static readonly string UserId = Guid.NewGuid().ToString();

    private static ProjectsController CreateController(Mock<IProjectsService> service)
        => new(service.Object) { ControllerContext = TestHelpers.ControllerContextFor(UserId) };

    // GetProjects

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

        var controller = CreateController(service);
        var result = await controller.GetProjects(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(projects, ok.Value);
    }

    [Fact]
    public async Task GetProjects_EmptyList_ReturnsOkWithEmptyList()
    {
        var service = new Mock<IProjectsService>();
        service.Setup(x => x.GetProjectsAsync(UserId, default)).ReturnsAsync(new List<ProjectDto>());

        var controller = CreateController(service);
        var result = await controller.GetProjects(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsType<List<ProjectDto>>(ok.Value);
        Assert.Empty(list);
    }

    // GetProject

    [Fact]
    public async Task GetProject_NotFound_Returns404()
    {
        var id = Guid.NewGuid();
        var service = new Mock<IProjectsService>();
        service.Setup(x => x.GetProjectAsync(id, UserId, default))
            .ReturnsAsync(Errors.NotFound("Project", id));

        var controller = CreateController(service);
        var result = await controller.GetProject(id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetProject_Success_ReturnsOkWithProjectDetails()
    {
        var id = Guid.NewGuid();
        var projectWithMembers = new ProjectWithMembersDto(
            id, "My Project", "description", "proj_key",
            [new ProjectMemberDto(UserId, "owner@acme.dev")]);

        var service = new Mock<IProjectsService>();
        service.Setup(x => x.GetProjectAsync(id, UserId, default))
            .ReturnsAsync(projectWithMembers);

        var controller = CreateController(service);
        var result = await controller.GetProject(id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ProjectWithMembersDto>(ok.Value);
        Assert.Equal(id, dto.Id);
        Assert.Single(dto.Members);
    }

    // CreateProject

    [Fact]
    public async Task CreateProject_Success_Returns201WithProject()
    {
        var id = Guid.NewGuid();
        var created = new ProjectDto(id, "New Project", "A description", "proj_newkey");

        var service = new Mock<IProjectsService>();
        service.Setup(x => x.CreateProjectAsync(UserId, "New Project", "A description", default))
            .ReturnsAsync(created);

        var controller = CreateController(service);
        var result = await controller.CreateProject(
            new CreateProjectRequest { Name = "New Project", Description = "A description" },
            CancellationToken.None);

        var created201 = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, created201.StatusCode);
        var dto = Assert.IsType<ProjectDto>(created201.Value);
        Assert.Equal(id, dto.Id);
    }

    [Fact]
    public async Task CreateProject_UserNotFound_Returns404()
    {
        var service = new Mock<IProjectsService>();
        service.Setup(x => x.CreateProjectAsync(UserId, "X", string.Empty, default))
            .ReturnsAsync(Errors.NotFound("User", UserId));

        var controller = CreateController(service);
        var result = await controller.CreateProject(
            new CreateProjectRequest { Name = "X" },
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    // UpdateProject

    [Fact]
    public async Task UpdateProject_Success_ReturnsOkWithUpdatedProject()
    {
        var id = Guid.NewGuid();
        var updated = new ProjectDto(id, "Updated Name", "Updated desc", "proj_key");

        var service = new Mock<IProjectsService>();
        service.Setup(x => x.UpdateProjectAsync(id, UserId, "Updated Name", "Updated desc", default))
            .ReturnsAsync(updated);

        var controller = CreateController(service);
        var result = await controller.UpdateProject(
            id,
            new CreateProjectRequest { Name = "Updated Name", Description = "Updated desc" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ProjectDto>(ok.Value);
        Assert.Equal("Updated Name", dto.Name);
    }

    [Fact]
    public async Task UpdateProject_NotFound_Returns404()
    {
        var id = Guid.NewGuid();
        var service = new Mock<IProjectsService>();
        service.Setup(x => x.UpdateProjectAsync(id, UserId, It.IsAny<string>(), It.IsAny<string?>(), default))
            .ReturnsAsync(Errors.NotFound("Project", id));

        var controller = CreateController(service);
        var result = await controller.UpdateProject(
            id,
            new CreateProjectRequest { Name = "Any" },
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    // DeleteProject

    [Fact]
    public async Task DeleteProject_Success_Returns204()
    {
        var id = Guid.NewGuid();
        var service = new Mock<IProjectsService>();
        service.Setup(x => x.DeleteProjectAsync(id, UserId, default)).ReturnsAsync(Result.Success());

        var controller = CreateController(service);
        var result = await controller.DeleteProject(id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteProject_NotFound_Returns404()
    {
        var id = Guid.NewGuid();
        var service = new Mock<IProjectsService>();
        service.Setup(x => x.DeleteProjectAsync(id, UserId, default))
            .ReturnsAsync(Errors.NotFound("Project", id));

        var controller = CreateController(service);
        var result = await controller.DeleteProject(id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    // RegenerateApiKey

    [Fact]
    public async Task RegenerateApiKey_Success_ReturnsOkWithNewKey()
    {
        var id = Guid.NewGuid();
        var dto = new ProjectDto(id, "Project", "desc", "proj_newkey");

        var service = new Mock<IProjectsService>();
        service.Setup(x => x.RegenerateApiKeyAsync(id, UserId, default)).ReturnsAsync(dto);

        var controller = CreateController(service);
        var result = await controller.RegenerateApiKey(id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<ProjectDto>(ok.Value);
        Assert.Equal("proj_newkey", returned.ApiKey);
    }

    [Fact]
    public async Task RegenerateApiKey_NotFound_Returns404()
    {
        var id = Guid.NewGuid();
        var service = new Mock<IProjectsService>();
        service.Setup(x => x.RegenerateApiKeyAsync(id, UserId, default))
            .ReturnsAsync(Errors.NotFound("Project", id));

        var controller = CreateController(service);
        var result = await controller.RegenerateApiKey(id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    // AddMember

    [Fact]
    public async Task AddMember_AlreadyMember_Returns400()
    {
        var id = Guid.NewGuid();
        var service = new Mock<IProjectsService>();
        service.Setup(x => x.AddMemberAsync(id, UserId, "dup@example.com", default))
            .ReturnsAsync(Errors.Conflict("User is already a member of this project"));

        var controller = CreateController(service);
        var result = await controller.AddMember(id, new() { Email = "dup@example.com" }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task AddMember_Success_ReturnsOk()
    {
        var id = Guid.NewGuid();
        var service = new Mock<IProjectsService>();
        service.Setup(x => x.AddMemberAsync(id, UserId, "new@example.com", default))
            .ReturnsAsync(Result.Success());

        var controller = CreateController(service);
        var result = await controller.AddMember(id, new AddMemberRequest { Email = "new@example.com" }, CancellationToken.None);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task AddMember_UserNotFound_ReturnsBadRequest()
    {
        var id = Guid.NewGuid();
        var service = new Mock<IProjectsService>();
        service.Setup(x => x.AddMemberAsync(id, UserId, "ghost@example.com", default))
            .ReturnsAsync(Errors.Validation("Email", "User not found"));

        var controller = CreateController(service);
        var result = await controller.AddMember(id, new AddMemberRequest { Email = "ghost@example.com" }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task AddMember_ProjectNotFound_Returns404()
    {
        var id = Guid.NewGuid();
        var service = new Mock<IProjectsService>();
        service.Setup(x => x.AddMemberAsync(id, UserId, "member@example.com", default))
            .ReturnsAsync(Errors.NotFound("Project", id));

        var controller = CreateController(service);
        var result = await controller.AddMember(id, new AddMemberRequest { Email = "member@example.com" }, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    // RemoveMember

    [Fact]
    public async Task RemoveMember_Success_Returns204()
    {
        var id = Guid.NewGuid();
        var memberId = Guid.NewGuid().ToString();
        var service = new Mock<IProjectsService>();
        service.Setup(x => x.RemoveMemberAsync(id, UserId, memberId, default))
            .ReturnsAsync(Result.Success());

        var controller = CreateController(service);
        var result = await controller.RemoveMember(id, memberId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task RemoveMember_MemberNotFound_Returns404()
    {
        var id = Guid.NewGuid();
        var memberId = Guid.NewGuid().ToString();
        var service = new Mock<IProjectsService>();
        service.Setup(x => x.RemoveMemberAsync(id, UserId, memberId, default))
            .ReturnsAsync(Errors.NotFound("Member", memberId));

        var controller = CreateController(service);
        var result = await controller.RemoveMember(id, memberId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task RemoveMember_ProjectNotFound_Returns404()
    {
        var id = Guid.NewGuid();
        var memberId = Guid.NewGuid().ToString();
        var service = new Mock<IProjectsService>();
        service.Setup(x => x.RemoveMemberAsync(id, UserId, memberId, default))
            .ReturnsAsync(Errors.NotFound("Project", id));

        var controller = CreateController(service);
        var result = await controller.RemoveMember(id, memberId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
