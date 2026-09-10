using System.ComponentModel.DataAnnotations;
using System.Reflection;
using CMS.API.Controllers;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests.Controllers;

public class AppUsersControllerTests
{
    private readonly Mock<IAppUserRepository> _repository = new(MockBehavior.Strict);
    private readonly Mock<IPasswordStampCache> _passwordStamps = new(MockBehavior.Strict);
    private readonly AppUsersController _controller;

    public AppUsersControllerTests()
    {
        _passwordStamps.Setup(c => c.Invalidate(It.IsAny<string>()));
        _controller = new AppUsersController(_repository.Object, _passwordStamps.Object);
    }

    private static AppUser Sample(string userId = "helen") => new()
    {
        Pkid = 1,
        UserId = userId,
        UserName = "Helen Chen",
        IsActive = true,
        PasswordUpdatedTime = new DateTime(2026, 9, 1, 8, 0, 0),
        RoleCount = 2,
        RoleIds = ["Admin", "Editor"]
    };

    private static AppUserRequest SampleRequest(string userId = "helen") => new()
    {
        UserId = userId,
        UserName = "Helen Chen",
        IsActive = true,
        RoleIds = ["Admin", "Editor"]
    };

    // ---- password hash must never cross the API boundary ----

    [Fact]
    public void ResponseAndRequestModels_DoNotExposePasswordHash()
    {
        static IEnumerable<string> Names(Type t) =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name);

        Assert.DoesNotContain(Names(typeof(AppUser)), n => n.Contains("Password", StringComparison.OrdinalIgnoreCase) && n != nameof(AppUser.PasswordUpdatedTime));
        Assert.DoesNotContain(Names(typeof(AppUserRequest)), n => n.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    // ---- GET /api/app-users ----

    [Fact]
    public async Task GetAll_ReturnsOk_WithRepositoryList()
    {
        var expected = new List<AppUser> { Sample("helen"), Sample("miles") };
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _controller.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
    }

    // ---- POST /api/app-users/query ----

    [Fact]
    public async Task Query_PassesFilterThrough_AndReturnsOk()
    {
        var query = new AppUserQuery { Keyword = "hel", IsActive = true, RoleId = "Admin" };
        var expected = new List<AppUser> { Sample() };
        _repository.Setup(r => r.QueryAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _controller.Query(query, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
        _repository.Verify(r => r.QueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- GET /api/app-users/{id} ----

    [Fact]
    public async Task GetById_UsesStringKey_AndReturnsOk()
    {
        var item = Sample("miles@uuu.com.tw");
        _repository.Setup(r => r.GetByIdAsync("miles@uuu.com.tw", It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await _controller.GetById("miles@uuu.com.tw", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(item, ok.Value);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenMissing()
    {
        _repository.Setup(r => r.GetByIdAsync("nobody", It.IsAny<CancellationToken>())).ReturnsAsync((AppUser?)null);

        var result = await _controller.GetById("nobody", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---- POST /api/app-users ----

    [Fact]
    public async Task Create_ReturnsCreatedAtGetById_KeyedByUserId()
    {
        var request = SampleRequest("  newuser  ");
        _repository.Setup(r => r.ExistsAsync("newuser", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _repository.Setup(r => r.CreateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(7);

        var result = await _controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal(nameof(AppUsersController.GetById), created.ActionName);
        Assert.Equal("newuser", created.RouteValues!["id"]);

        var body = Assert.IsType<AppUser>(created.Value);
        Assert.Equal(7, body.Pkid);
        Assert.Equal("newuser", body.UserId);
        Assert.Equal("Helen Chen", body.UserName);
        Assert.True(body.IsActive);
        Assert.Equal(2, body.RoleCount);
        Assert.NotNull(body.PasswordUpdatedTime);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenUserIdExists()
    {
        var request = SampleRequest("helen");
        _repository.Setup(r => r.ExistsAsync("helen", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _controller.Create(request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        _repository.Verify(r => r.CreateAsync(It.IsAny<AppUserRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_Returns500Problem_WhenDefaultPasswordIsUnavailable()
    {
        var request = SampleRequest("newuser");
        _repository.Setup(r => r.ExistsAsync("newuser", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _repository.Setup(r => r.CreateAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AppConfigException("SysConfig 缺少 appConfig"));

        var result = await _controller.Create(request, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("SysConfig 缺少 appConfig", details.Detail);
    }

    // ---- PUT /api/app-users ----

    [Fact]
    public async Task Update_ReturnsNoContent_WhenRowUpdated()
    {
        var request = SampleRequest("helen");
        _repository.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _controller.Update(request, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenNoRowMatched()
    {
        var request = SampleRequest("ghost");
        _repository.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _controller.Update(request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    // ---- DELETE /api/app-users/{id} ----

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenDeleted()
    {
        _repository.Setup(r => r.DeleteAsync("helen", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _controller.Delete("helen", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        _repository.Setup(r => r.DeleteAsync("ghost", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _controller.Delete("ghost", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsConflict_WhenRowIsReferencedByForeignKey()
    {
        _repository.Setup(r => r.DeleteAsync("helen", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityInUseException("in use"));

        var result = await _controller.Delete("helen", CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    // ---- POST /api/app-users/{id}/reset-password ----

    [Fact]
    public async Task ResetPassword_ReturnsNoContent_WhenReset()
    {
        _repository.Setup(r => r.ResetPasswordAsync("helen", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _controller.ResetPassword("helen", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    /// <summary>
    /// The reset writes PasswordUpdatedTime, which is the token-revocation stamp, but the bearer handler
    /// reads that through a per-user cache. Without dropping the cached entry the target's existing token
    /// kept working for up to PasswordStampCache.CacheDuration — and this reset is the one lever an admin
    /// has to end a hijacked session. AuthController.ChangePassword has always done this.
    /// </summary>
    [Fact]
    public async Task ResetPassword_InvalidatesTheCachedPasswordStamp_SoTheTargetsTokenDiesOnTheNextRequest()
    {
        _repository.Setup(r => r.ResetPasswordAsync("helen", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await _controller.ResetPassword("helen", CancellationToken.None);

        _passwordStamps.Verify(c => c.Invalidate("helen"), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_ReturnsNotFound_WhenMissing()
    {
        _repository.Setup(r => r.ResetPasswordAsync("ghost", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _controller.ResetPassword("ghost", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ResetPassword_DoesNotInvalidateAnything_WhenTheUserDoesNotExist()
    {
        _repository.Setup(r => r.ResetPasswordAsync("ghost", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await _controller.ResetPassword("ghost", CancellationToken.None);

        _passwordStamps.Verify(c => c.Invalidate(It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// Create checks ExistsAsync first, but that is a read followed by a write: two callers racing on the
    /// same UserId both pass it and the second loses on the primary key. The table stays correct — the
    /// constraint does its job — but the loser used to surface as a 500 rather than the documented 409.
    /// </summary>
    [Fact]
    public async Task Create_Returns409_WhenAConcurrentCreateWonTheRace()
    {
        var request = SampleRequest();
        _repository.Setup(r => r.ExistsAsync("helen", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _repository.Setup(r => r.CreateAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicateKeyException("帳號「helen」已存在。"));

        var result = await _controller.Create(request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_Returns500Problem_WhenDefaultPasswordIsUnavailable()
    {
        _repository.Setup(r => r.ResetPasswordAsync("helen", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AppConfigException("bad config"));

        var result = await _controller.ResetPassword("helen", CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
    }

    // ---- Request validation ----

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Request_IsValid_WithRequiredFields_AndDefaultsIsActiveToTrue()
    {
        var request = new AppUserRequest { UserId = "helen", UserName = "Helen" };

        Assert.True(request.IsActive);
        Assert.Empty(Validate(request));
    }

    [Theory]
    [InlineData("", "Helen")]
    [InlineData("   ", "Helen")]
    [InlineData("helen", "")]
    [InlineData("helen", "   ")]
    public void Request_IsInvalid_WhenUserIdOrUserNameMissing(string userId, string userName)
    {
        var request = new AppUserRequest { UserId = userId, UserName = userName };

        Assert.NotEmpty(Validate(request));
    }

    [Theory]
    [InlineData(nameof(AppUserRequest.UserId))]
    [InlineData(nameof(AppUserRequest.UserName))]
    public void Request_IsInvalid_WhenStringsExceed200Chars(string member)
    {
        var request = SampleRequest();
        typeof(AppUserRequest).GetProperty(member)!.SetValue(request, new string('名', 201));

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(member));
    }
}
