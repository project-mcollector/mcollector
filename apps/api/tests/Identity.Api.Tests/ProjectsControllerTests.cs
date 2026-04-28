using System.Security.Claims;
using Identity.Api.Api.Controllers;
using Identity.Api.Application.Services;
using Identity.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Identity.Api.Tests;

public class ProjectsControllerTests
{
    [Fact]
    public async Task GetProjects_ReturnsOnlyOwnedProjects()
    {
        await using var dbContext = TestHelpers.CreateDbContext();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = "owner@acme.dev",
            UserName = "owner@acme.dev"
        };
        var ownedProject = new Project { Name = "Owned", Description = "Owned project" };
        var otherProject = new Project { Name = "Other", Description = "Other project" };

        user.Projects.Add(ownedProject);
        dbContext.Users.Add(user);
        dbContext.Projects.AddRange(ownedProject, otherProject);
        await dbContext.SaveChangesAsync();

        var userManager = TestHelpers.CreateUserManagerMock();
        userManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

        var controller = new ProjectsController(userManager.Object, dbContext, Mock.Of<IApiKeyService>())
        {
            ControllerContext = new()
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new(new ClaimsIdentity([
                        new(ClaimTypes.NameIdentifier, user.Id)
                    ], "TestAuth"))
                }
            }
        };

        var result = await controller.GetProjects();

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<List<ProjectResponse>>(ok.Value);
        Assert.Single(response);
        Assert.Equal(ownedProject.Id, response[0].Id);
        Assert.Equal(ownedProject.Name, response[0].Name);
    }

    [Fact]
    public async Task CreateProject_CreatesProjectAndAssignsUser()
    {
        await using var dbContext = TestHelpers.CreateDbContext();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = "owner@acme.dev",
            UserName = "owner@acme.dev"
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var userManager = TestHelpers.CreateUserManagerMock();
        userManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

        var apiKeyService = new Mock<IApiKeyService>();
        apiKeyService.Setup(x => x.GenerateApiKey()).Returns("proj_test_api_key");

        var controller = new ProjectsController(userManager.Object, dbContext, apiKeyService.Object)
        {
            ControllerContext = new()
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new(new ClaimsIdentity([
                        new(ClaimTypes.NameIdentifier, user.Id)
                    ], "TestAuth"))
                }
            }
        };

        var result = await controller.CreateProject(new()
        {
            Name = "New project",
            Description = "Created in test"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ProjectResponse>(ok.Value);
        Assert.Equal("proj_test_api_key", response.ApiKey);
        Assert.Equal("New project", response.Name);

        var storedProject = await dbContext.Projects.Include(p => p.Users).SingleAsync();
        Assert.Equal("New project", storedProject.Name);
        Assert.Single(storedProject.Users);
        Assert.Equal(user.Id, storedProject.Users.Single().Id);
    }

    [Fact]
    public async Task GetProject_UserWithoutAccess_ReturnsNotFound()
    {
        await using var dbContext = TestHelpers.CreateDbContext();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = "owner@acme.dev",
            UserName = "owner@acme.dev"
        };
        var project = new Project { Name = "Private", Description = "Private project" };

        dbContext.Users.Add(user);
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();

        var userManager = TestHelpers.CreateUserManagerMock();
        userManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

        var controller = new ProjectsController(userManager.Object, dbContext, Mock.Of<IApiKeyService>())
        {
            ControllerContext = new()
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new(new ClaimsIdentity([
                        new Claim(ClaimTypes.NameIdentifier, user.Id)
                    ], "TestAuth"))
                }
            }
        };

        var result = await controller.GetProject(project.Id);

        Assert.IsType<NotFoundResult>(result);
    }
}


