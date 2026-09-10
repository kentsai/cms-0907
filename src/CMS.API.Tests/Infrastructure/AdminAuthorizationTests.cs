using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using CMS.API.Controllers;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CMS.API.Tests.Infrastructure;

/// <summary>
/// End-to-end: the endpoints that hand out access itself require the <c>Admin</c> role, not merely a valid token.
/// Before this policy existed every signed-in user could create accounts, delete them, rewrite role membership and
/// reset any password to the shared system default — the Angular shell's hidden 系統管理 Admin menu was the only
/// thing in the way, and a menu is not a control.
/// </summary>
public class AdminAuthorizationTests
{
    private const string AppUsersUrl = "/api/app-users";
    private const string AppRolesUrl = "/api/app-roles";
    private const string ResetPasswordUrl = "/api/app-users/victim/reset-password";
    private const string AppUsersLookupUrl = "/api/lookups/app-users";
    private const string AppRolesLookupUrl = "/api/lookups/app-roles";
    /// <summary>A content endpoint: authenticated is enough, no role needed.</summary>
    private const string ContentUrl = "/api/publish-statuses";

    private static HttpClient ClientWithToken(CmsApiFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>A signed-in user who is not an administrator — the exact starting position of the escalation chain.</summary>
    private static HttpClient EditorClient(CmsApiFactory factory) =>
        ClientWithToken(factory, CmsApiFactory.IssueToken(roles: ["Editor"]));

    private static HttpClient AdminClient(CmsApiFactory factory) =>
        ClientWithToken(factory, CmsApiFactory.IssueToken(roles: [AuthorizationPolicies.AdminRole]));

    private static CmsApiFactory FactoryWithAdminRepositories()
    {
        var factory = new CmsApiFactory();
        var users = new Mock<IAppUserRepository>();
        users.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        users.Setup(r => r.GetLookupAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        users.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        users.Setup(r => r.CreateAsync(It.IsAny<AppUserRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        users.Setup(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        users.Setup(r => r.ResetPasswordAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var roles = new Mock<IAppRoleRepository>();
        roles.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        roles.Setup(r => r.GetLookupAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        factory.AppUserRepository = users;
        factory.AppRoleRepository = roles;
        return factory;
    }

    // ---- a signed-in non-administrator is refused, and the repository is never reached ----

    [Fact]
    public async Task Editor_CannotListAccounts()
    {
        using var factory = FactoryWithAdminRepositories();

        var response = await EditorClient(factory).GetAsync(AppUsersUrl);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        factory.AppUserRepository!.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Editor_CannotCreateAnAccount_SoCannotGrantItselfAdmin()
    {
        using var factory = FactoryWithAdminRepositories();

        var response = await EditorClient(factory).PostAsJsonAsync(AppUsersUrl, new AppUserRequest
        {
            UserId = "attacker",
            UserName = "attacker",
            IsActive = true,
            RoleIds = [AuthorizationPolicies.AdminRole]
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        factory.AppUserRepository!.Verify(r => r.CreateAsync(It.IsAny<AppUserRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Editor_CannotResetAnotherUsersPasswordToTheSystemDefault()
    {
        using var factory = FactoryWithAdminRepositories();

        var response = await EditorClient(factory).PostAsync(ResetPasswordUrl, null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        factory.AppUserRepository!.Verify(r => r.ResetPasswordAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Editor_CannotDeleteAnAccount()
    {
        using var factory = FactoryWithAdminRepositories();

        var response = await EditorClient(factory).DeleteAsync($"{AppUsersUrl}/victim");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        factory.AppUserRepository!.Verify(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Editor_CannotRewriteRoleMembership()
    {
        using var factory = FactoryWithAdminRepositories();

        var response = await EditorClient(factory).PutAsJsonAsync(AppRolesUrl, new AppRoleRequest
        {
            RoleId = AuthorizationPolicies.AdminRole,
            RoleName = "Admin",
            UserIds = [CmsApiFactory.UserId]
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        factory.AppRoleRepository!.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(AppUsersLookupUrl)]
    [InlineData(AppRolesLookupUrl)]
    public async Task Editor_CannotEnumerateAccountsOrRolesThroughTheLookups(string url)
    {
        using var factory = FactoryWithAdminRepositories();

        var response = await EditorClient(factory).GetAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UserWithNoRolesAtAll_IsRefusedToo()
    {
        using var factory = FactoryWithAdminRepositories();
        var client = ClientWithToken(factory, CmsApiFactory.IssueToken());

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(AppUsersUrl)).StatusCode);
    }

    // ---- the boundary is exactly where it should be ----

    [Fact]
    public async Task Administrator_StillReachesTheAdminEndpoints()
    {
        using var factory = FactoryWithAdminRepositories();
        var client = AdminClient(factory);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(AppUsersUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(AppRolesUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(AppUsersLookupUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(AppRolesLookupUrl)).StatusCode);
    }

    [Fact]
    public async Task Editor_KeepsFullAccessToTheContentTables()
    {
        using var factory = FactoryWithAdminRepositories();

        var response = await EditorClient(factory).GetAsync(ContentUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NoToken_IsStill401_NotForbidden_OnAnAdminEndpoint()
    {
        using var factory = FactoryWithAdminRepositories();

        var response = await factory.CreateClient().GetAsync(AppUsersUrl);

        // Authentication must win over authorization, so an expired session is told to sign in again.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdministratorWhoMustChangeTheirPassword_IsStillLockedOutOfTheAdminEndpoints()
    {
        using var factory = FactoryWithAdminRepositories();
        var client = ClientWithToken(factory, CmsApiFactory.IssueMustChangePasswordToken(AuthorizationPolicies.AdminRole));

        var response = await client.GetAsync(AppUsersUrl);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        factory.AppUserRepository!.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- the policy itself ----

    [Fact]
    public async Task AdminPolicy_IsRegistered_AndRequiresTheAdminRole()
    {
        using var factory = new CmsApiFactory();
        var policy = await factory.Services.GetRequiredService<IAuthorizationPolicyProvider>()
            .GetPolicyAsync(AuthorizationPolicies.Admin);

        Assert.NotNull(policy);
        var requirement = Assert.Single(policy.Requirements.OfType<RolesAuthorizationRequirement>());
        Assert.Equal([AuthorizationPolicies.AdminRole], requirement.AllowedRoles);
    }

    [Fact]
    public void EveryControllerThatHandsOutAccess_CarriesTheAdminPolicy()
    {
        var guarded = typeof(Program).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .Where(t => t.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                .Any(a => a.Policy == AuthorizationPolicies.Admin))
            .Select(t => t.Name)
            .OrderBy(name => name)
            .ToList();

        Assert.Equal([nameof(AppRolesController), nameof(AppUsersController)], guarded);
    }

    [Fact]
    public void TheAdminOnlyLookups_AreExactlyTheAccountAndRoleOnes()
    {
        var guarded = typeof(LookupsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                .Any(a => a.Policy == AuthorizationPolicies.Admin))
            .Select(m => m.Name)
            .OrderBy(name => name)
            .ToList();

        Assert.Equal([nameof(LookupsController.AppRoles), nameof(LookupsController.AppUsers)], guarded);
    }
}
