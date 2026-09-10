using System.Security.Claims;
using CMS.API.Controllers;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CMS.API.Tests.Controllers;

/// <summary>Unit tests for <c>PUT /api/auth/profile</c>: the user always comes from the principal, never the body.</summary>
public class AuthControllerProfileTests
{
    private readonly Mock<IAuthRepository> _repository = new(MockBehavior.Strict);
    private readonly AuthController _controller;

    public AuthControllerProfileTests()
    {
        _controller = new AuthController(
            _repository.Object, Mock.Of<IJwtTokenIssuer>(), TimeProvider.System, Mock.Of<IPasswordStampCache>(),
            NullLogger<AuthController>.Instance);
    }

    /// <summary>Mimics what the bearer handler produces: a <c>userId</c> claim plus one role claim per role.</summary>
    private void SignInAs(string userId, params string[] roles)
    {
        var claims = new List<Claim> { new(JwtTokenIssuer.UserIdClaim, userId) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        var identity = new ClaimsIdentity(claims, "Bearer", JwtTokenIssuer.UserIdClaim, ClaimTypes.Role);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private void SetupUpdate(string userId, string userName, bool result = true) =>
        _repository.Setup(r => r.UpdateUserNameAsync(userId, userName, It.IsAny<CancellationToken>())).ReturnsAsync(result);

    [Fact]
    public async Task UpdateProfile_UpdatesUserNameForTheJwtUser_AndReturnsTheProfile()
    {
        SignInAs("helen", "Admin", "Editor");
        SetupUpdate("helen", "Helen Wang");

        var result = await _controller.UpdateProfile(new UpdateProfileRequest { UserName = "Helen Wang" }, CancellationToken.None);

        var body = Assert.IsType<ProfileResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("helen", body.UserId);
        Assert.Equal("Helen Wang", body.UserName);
        Assert.Equal(["Admin", "Editor"], body.Roles);
        _repository.Verify(r => r.UpdateUserNameAsync("helen", "Helen Wang", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProfile_TrimsTheUserName()
    {
        SignInAs("helen");
        SetupUpdate("helen", "Helen Wang");

        var result = await _controller.UpdateProfile(new UpdateProfileRequest { UserName = "\t Helen Wang \n" }, CancellationToken.None);

        var body = Assert.IsType<ProfileResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("Helen Wang", body.UserName);
    }

    [Fact]
    public async Task UpdateProfile_UsesTheTokenUserId_NotAnythingTheCallerSupplies()
    {
        // The request type has no UserId member at all, so the only possible source is the principal.
        Assert.Null(typeof(UpdateProfileRequest).GetProperty("UserId"));
        Assert.Null(typeof(UpdateProfileRequest).GetProperty("RoleIds"));

        SignInAs("helen");
        SetupUpdate("helen", "X");

        await _controller.UpdateProfile(new UpdateProfileRequest { UserName = "X" }, CancellationToken.None);

        _repository.Verify(r => r.UpdateUserNameAsync("helen", "X", It.IsAny<CancellationToken>()), Times.Once);
        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    [InlineData(null)]
    public async Task UpdateProfile_Returns400_ForEmptyOrWhitespaceUserName(string? userName)
    {
        SignInAs("helen");

        var result = await _controller.UpdateProfile(new UpdateProfileRequest { UserName = userName! }, CancellationToken.None);

        var problem = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        var details = Assert.IsType<ValidationProblemDetails>(problem.Value);
        Assert.Contains(AuthController.UserNameRequiredMessage, details.Errors[nameof(UpdateProfileRequest.UserName)]);
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateProfile_Returns404_WhenTheUserRowIsGone()
    {
        SignInAs("helen");
        SetupUpdate("helen", "Helen Wang", result: false);

        var result = await _controller.UpdateProfile(new UpdateProfileRequest { UserName = "Helen Wang" }, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpdateProfile_Returns401_WhenThePrincipalHasNoUserId()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        var result = await _controller.UpdateProfile(new UpdateProfileRequest { UserName = "Helen Wang" }, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
        _repository.VerifyNoOtherCalls();
    }
}
