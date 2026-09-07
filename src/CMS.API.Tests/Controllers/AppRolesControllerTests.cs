using System.ComponentModel.DataAnnotations;
using CMS.API.Controllers;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests.Controllers;

public class AppRolesControllerTests
{
    private readonly Mock<IAppRoleRepository> _repository = new(MockBehavior.Strict);
    private readonly AppRolesController _controller;

    public AppRolesControllerTests()
    {
        _controller = new AppRolesController(_repository.Object);
    }

    private static AppRole Sample(string roleId = "Admin") => new()
    {
        Pkid = 1,
        RoleId = roleId,
        RoleName = "Administrator",
        PermissionLevel = 1,
        Description = "系統管理員",
        UserCount = 2,
        UserIds = ["helen", "miles"]
    };

    private static AppRoleRequest SampleRequest(string roleId = "Admin") => new()
    {
        RoleId = roleId,
        RoleName = "Administrator",
        PermissionLevel = 1,
        Description = "系統管理員",
        UserIds = ["helen", "miles"]
    };

    [Fact]
    public async Task GetAll_ReturnsOk_WithRepositoryList()
    {
        var expected = new List<AppRole> { Sample("Admin"), Sample("User") };
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _controller.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
    }

    [Fact]
    public async Task Query_PassesFilterThrough_AndReturnsOk()
    {
        var query = new AppRoleQuery { Keyword = "Adm", PermissionLevel = 1, UserId = "helen" };
        var expected = new List<AppRole> { Sample() };
        _repository.Setup(r => r.QueryAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _controller.Query(query, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
    }

    [Fact]
    public async Task GetById_UsesStringKey_AndReturnsOk()
    {
        var item = Sample("Power User");
        _repository.Setup(r => r.GetByIdAsync("Power User", It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await _controller.GetById("Power User", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(item, ok.Value);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenMissing()
    {
        _repository.Setup(r => r.GetByIdAsync("nope", It.IsAny<CancellationToken>())).ReturnsAsync((AppRole?)null);

        var result = await _controller.GetById("nope", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtGetById_KeyedByRoleId()
    {
        var request = SampleRequest("Editor");
        _repository.Setup(r => r.ExistsAsync("Editor", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _repository.Setup(r => r.CreateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(7);

        var result = await _controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal(nameof(AppRolesController.GetById), created.ActionName);
        Assert.Equal("Editor", created.RouteValues!["id"]);

        var body = Assert.IsType<AppRole>(created.Value);
        Assert.Equal(7, body.Pkid);
        Assert.Equal("Editor", body.RoleId);
        Assert.Equal(2, body.UserCount);
    }

    [Fact]
    public async Task Create_TrimsRoleId_BeforeExistenceCheck()
    {
        var request = SampleRequest("  Editor  ");
        _repository.Setup(r => r.ExistsAsync("Editor", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _repository.Setup(r => r.CreateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(7);

        await _controller.Create(request, CancellationToken.None);

        Assert.Equal("Editor", request.RoleId);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenRoleIdExists()
    {
        var request = SampleRequest("Admin");
        _repository.Setup(r => r.ExistsAsync("Admin", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _controller.Create(request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        _repository.Verify(r => r.CreateAsync(It.IsAny<AppRoleRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_ReturnsNoContent_WhenRowUpdated()
    {
        var request = SampleRequest();
        _repository.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        Assert.IsType<NoContentResult>(await _controller.Update(request, CancellationToken.None));
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenNoRowMatched()
    {
        var request = SampleRequest("ghost");
        _repository.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        Assert.IsType<NotFoundResult>(await _controller.Update(request, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenDeleted()
    {
        _repository.Setup(r => r.DeleteAsync("Admin", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        Assert.IsType<NoContentResult>(await _controller.Delete("Admin", CancellationToken.None));
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        _repository.Setup(r => r.DeleteAsync("ghost", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        Assert.IsType<NotFoundResult>(await _controller.Delete("ghost", CancellationToken.None));
    }

    [Fact]
    public async Task Delete_ReturnsConflict_WhenRowIsReferencedByForeignKey()
    {
        _repository.Setup(r => r.DeleteAsync("Admin", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityInUseException("in use"));

        var conflict = Assert.IsType<ConflictObjectResult>(await _controller.Delete("Admin", CancellationToken.None));
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    // ---- Request validation ----

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Request_IsValid_WithRequiredFields_AndDefaultsPermissionLevelTo100()
    {
        var request = new AppRoleRequest { RoleId = "User", RoleName = "User" };

        Assert.Empty(Validate(request));
        Assert.Equal(100, request.PermissionLevel);
        Assert.Null(request.Description);
        Assert.Empty(request.UserIds);
    }

    [Theory]
    [InlineData("", "User")]
    [InlineData("   ", "User")]
    [InlineData("User", "")]
    [InlineData("User", "   ")]
    public void Request_IsInvalid_WhenRoleIdOrRoleNameMissing(string roleId, string roleName)
    {
        var request = new AppRoleRequest { RoleId = roleId, RoleName = roleName };

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void Request_IsInvalid_WhenStringsExceedColumnLengths()
    {
        var request = new AppRoleRequest
        {
            RoleId = new string('r', 201),
            RoleName = new string('n', 201),
            Description = new string('d', 401)
        };

        var members = Validate(request).SelectMany(e => e.MemberNames).ToList();

        Assert.Contains(nameof(AppRoleRequest.RoleId), members);
        Assert.Contains(nameof(AppRoleRequest.RoleName), members);
        Assert.Contains(nameof(AppRoleRequest.Description), members);
    }
}
