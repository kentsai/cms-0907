using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using CMS.API.Controllers;
using CMS.API.Infrastructure;
using CMS.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace CMS.API.Tests.Infrastructure;

/// <summary>
/// End-to-end: the bearer middleware + global authorization filter in front of a real controller.
/// <c>GET /api/publish-statuses</c> stands in for "any protected endpoint".
/// </summary>
public class JwtBearerAuthorizationTests
{
    private const string ProtectedUrl = "/api/publish-statuses";
    private const string LoginUrl = "/api/auth/login";

    private static HttpClient ClientWithToken(CmsApiFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ---- protected endpoints ----

    [Fact]
    public async Task ProtectedEndpoint_Returns401_WithoutBearerToken()
    {
        using var factory = new CmsApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(ProtectedUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
        factory.PublishStatusRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns200_WithValidBearerToken()
    {
        using var factory = new CmsApiFactory();
        var client = ClientWithToken(factory, CmsApiFactory.IssueToken(roles: ["Admin"]));

        var response = await client.GetAsync(ProtectedUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<List<PublishStatus>>();
        Assert.NotNull(items);
        Assert.Equal("草稿", Assert.Single(items).Description);
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns401_WithTokenSignedByAnotherKey()
    {
        using var factory = new CmsApiFactory();
        var client = ClientWithToken(factory, CmsApiFactory.IssueToken(signingKey: "some-other-key-that-is-also-long-enough-for-hs256"));

        var response = await client.GetAsync(ProtectedUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns401_WithExpiredToken()
    {
        using var factory = new CmsApiFactory();
        var twoDaysAgo = new FixedTimeProvider(DateTimeOffset.UtcNow.AddDays(-2));
        var client = ClientWithToken(factory, CmsApiFactory.IssueToken(clock: twoDaysAgo));

        var response = await client.GetAsync(ProtectedUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("")]
    public async Task ProtectedEndpoint_Returns401_WithMalformedToken(string token)
    {
        using var factory = new CmsApiFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {token}");

        var response = await client.GetAsync(ProtectedUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns401_NotFailure_WhenSigningKeyCannotBeLoaded()
    {
        using var factory = new CmsApiFactory();
        factory.AuthRepository
            .Setup(r => r.GetSymmetricSecurityKeyAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AppConfigException("SysConfig 缺少「appConfig」設定。"));
        var client = ClientWithToken(factory, CmsApiFactory.IssueToken());

        var response = await client.GetAsync(ProtectedUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- login stays anonymous ----

    [Fact]
    public async Task Login_IsReachableWithoutBearerToken_AndItsTokenOpensProtectedEndpoints()
    {
        using var factory = new CmsApiFactory();
        var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync(LoginUrl, new LoginRequest { UserId = CmsApiFactory.UserId, Password = CmsApiFactory.Password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var profile = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(profile);
        Assert.Equal(CmsApiFactory.UserId, profile.UserId);
        Assert.Equal(CmsApiFactory.UserName, profile.UserName);

        var protectedResponse = await ClientWithToken(factory, profile.AccessToken).GetAsync(ProtectedUrl);
        Assert.Equal(HttpStatusCode.OK, protectedResponse.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401FromTheController_NotTheMiddleware()
    {
        using var factory = new CmsApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(LoginUrl, new LoginRequest { UserId = CmsApiFactory.UserId, Password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        // The controller's generic body proves the request reached the action instead of being rejected by the bearer handler.
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal(AuthController.InvalidCredentialsMessage, body?["message"]);
    }

    [Fact]
    public async Task Login_IgnoresAnInvalidBearerHeader()
    {
        using var factory = new CmsApiFactory();
        var client = ClientWithToken(factory, "stale-garbage");

        var response = await client.PostAsJsonAsync(LoginUrl, new LoginRequest { UserId = CmsApiFactory.UserId, Password = CmsApiFactory.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- wiring ----

    [Fact]
    public void GlobalAuthorizeFilter_RequiresAnAuthenticatedUser()
    {
        using var factory = new CmsApiFactory();
        var mvcOptions = factory.Services.GetRequiredService<IOptions<MvcOptions>>().Value;

        var filter = Assert.Single(mvcOptions.Filters.OfType<AuthorizeFilter>());
        Assert.NotNull(filter.Policy);
        Assert.Contains(filter.Policy.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public void Login_IsTheOnlyAnonymousAction_InTheWholeApi()
    {
        var controllers = typeof(Program).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .ToList();

        Assert.Contains(typeof(AuthController), controllers);

        foreach (var controller in controllers)
        {
            Assert.Null(controller.GetCustomAttribute<AllowAnonymousAttribute>());

            var actionsAllowingAnonymous = controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
                .Select(m => m.Name)
                .ToList();

            if (controller == typeof(AuthController))
            {
                Assert.Equal([nameof(AuthController.Login)], actionsAllowingAnonymous);
            }
            else
            {
                Assert.Empty(actionsAllowingAnonymous);
            }
        }
    }

    // ---- PUT /api/auth/profile (protected, user taken from the token) ----

    private const string ProfileUrl = "/api/auth/profile";

    [Fact]
    public async Task UpdateProfile_Returns401_WithoutBearerToken()
    {
        using var factory = new CmsApiFactory();

        var response = await factory.CreateClient().PutAsJsonAsync(ProfileUrl, new { userName = "New Name" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        factory.AuthRepository.Verify(
            r => r.UpdateUserNameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProfile_UpdatesTheTokenUser_AndIgnoresAUserIdInTheBody()
    {
        using var factory = new CmsApiFactory();
        var client = ClientWithToken(factory, CmsApiFactory.IssueToken(roles: ["Admin", "Editor"]));

        // A hostile / buggy client names somebody else and tries to add a role: both are unbound and ignored.
        var response = await client.PutAsJsonAsync(ProfileUrl, new
        {
            userId = "somebody-else",
            userName = "  Helen Wang  ",
            roleIds = new[] { "Admin", "SuperUser" }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        Assert.NotNull(profile);
        Assert.Equal(CmsApiFactory.UserId, profile.UserId);
        Assert.Equal("Helen Wang", profile.UserName);
        Assert.Equal(["Admin", "Editor"], profile.Roles);

        factory.AuthRepository.Verify(
            r => r.UpdateUserNameAsync(CmsApiFactory.UserId, "Helen Wang", It.IsAny<CancellationToken>()), Times.Once);
        factory.AuthRepository.Verify(
            r => r.UpdateUserNameAsync("somebody-else", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateProfile_Returns400_ForEmptyOrWhitespaceUserName(string userName)
    {
        using var factory = new CmsApiFactory();
        var client = ClientWithToken(factory, CmsApiFactory.IssueToken());

        var response = await client.PutAsJsonAsync(ProfileUrl, new { userName });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        factory.AuthRepository.Verify(
            r => r.UpdateUserNameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- password change signs existing sessions out ----

    private const string ChangePasswordUrl = "/api/auth/change-password";

    private static void SetupStamp(CmsApiFactory factory, DateTime? passwordUpdatedTime) =>
        factory.AuthRepository
            .Setup(r => r.GetPasswordStampAsync(CmsApiFactory.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordStamp { PasswordUpdatedTime = passwordUpdatedTime });

    [Fact]
    public async Task ProtectedEndpoint_Returns401_WithATokenIssuedBeforeThePasswordWasChanged()
    {
        using var factory = new CmsApiFactory();
        var oneHourAgo = new FixedTimeProvider(DateTimeOffset.UtcNow.AddHours(-1));
        SetupStamp(factory, DateTime.UtcNow.AddMinutes(-30));
        var client = ClientWithToken(factory, CmsApiFactory.IssueToken(clock: oneHourAgo, roles: ["Admin"]));

        var response = await client.GetAsync(ProtectedUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
        factory.PublishStatusRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns200_WithATokenIssuedAfterThePasswordWasChanged()
    {
        using var factory = new CmsApiFactory();
        SetupStamp(factory, DateTime.UtcNow.AddHours(-1));
        var client = ClientWithToken(factory, CmsApiFactory.IssueToken(roles: ["Admin"]));

        var response = await client.GetAsync(ProtectedUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns401_WhenTheTokenUserNoLongerExists()
    {
        using var factory = new CmsApiFactory();
        factory.AuthRepository
            .Setup(r => r.GetPasswordStampAsync(CmsApiFactory.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PasswordStamp?)null);
        var client = ClientWithToken(factory, CmsApiFactory.IssueToken(roles: ["Admin"]));

        var response = await client.GetAsync(ProtectedUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_KeepsTheSession_WhenThePasswordStampCannotBeRead()
    {
        // Signature and lifetime already passed; a transient DB failure must not sign everybody out.
        using var factory = new CmsApiFactory();
        factory.AuthRepository
            .Setup(r => r.GetPasswordStampAsync(CmsApiFactory.UserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));
        var client = ClientWithToken(factory, CmsApiFactory.IssueToken(roles: ["Admin"]));

        var response = await client.GetAsync(ProtectedUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_KillsTheCurrentToken_AndOnlyTheNewPasswordLogsInAgain()
    {
        using var factory = new CmsApiFactory();

        // Mutable "row": the mocked repository reflects the password change like the database would.
        var credential = CmsApiFactory.Credential();
        var stamp = new PasswordStamp { PasswordUpdatedTime = null };
        factory.AuthRepository
            .Setup(r => r.GetCredentialAsync(CmsApiFactory.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => credential);
        factory.AuthRepository
            .Setup(r => r.GetPasswordStampAsync(CmsApiFactory.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => stamp);
        factory.AuthRepository
            .Setup(r => r.UpdatePasswordAsync(CmsApiFactory.UserId, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback((string _, string hash, DateTime updatedAt, CancellationToken _) =>
            {
                credential = new AppUserCredential { UserId = credential.UserId, UserName = credential.UserName, IsActive = true, PasswordHash = hash };
                stamp = new PasswordStamp { PasswordUpdatedTime = updatedAt };
            })
            .ReturnsAsync(true);

        // A session that began a minute ago (so its iat is strictly before the change even at whole-second resolution).
        var oldToken = CmsApiFactory.IssueToken(clock: new FixedTimeProvider(DateTimeOffset.UtcNow.AddMinutes(-1)), roles: ["Admin"]);
        var oldSession = ClientWithToken(factory, oldToken);
        Assert.Equal(HttpStatusCode.OK, (await oldSession.GetAsync(ProtectedUrl)).StatusCode);

        const string newPassword = "Summer2026!";
        var change = await oldSession.PostAsJsonAsync(ChangePasswordUrl, new ChangePasswordRequest
        {
            CurrentPassword = CmsApiFactory.Password,
            NewPassword = newPassword,
            ConfirmNewPassword = newPassword
        });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        // The token that made the change is now dead — on the very next request, no cache delay.
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldSession.GetAsync(ProtectedUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldSession.PostAsJsonAsync(ChangePasswordUrl, new ChangePasswordRequest
        {
            CurrentPassword = newPassword, NewPassword = "Other2026!", ConfirmNewPassword = "Other2026!"
        })).StatusCode);

        // The old password no longer logs in; the new one does, and its token opens protected endpoints.
        var anonymous = factory.CreateClient();
        var staleLogin = await anonymous.PostAsJsonAsync(LoginUrl, new LoginRequest { UserId = CmsApiFactory.UserId, Password = CmsApiFactory.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, staleLogin.StatusCode);

        var freshLogin = await anonymous.PostAsJsonAsync(LoginUrl, new LoginRequest { UserId = CmsApiFactory.UserId, Password = newPassword });
        Assert.Equal(HttpStatusCode.OK, freshLogin.StatusCode);
        var profile = await freshLogin.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(profile);

        Assert.Equal(HttpStatusCode.OK, (await ClientWithToken(factory, profile.AccessToken).GetAsync(ProtectedUrl)).StatusCode);
    }
}
