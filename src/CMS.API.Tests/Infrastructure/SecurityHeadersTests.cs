using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.API.Infrastructure;
using CMS.API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Net.Http.Headers;
using Moq;

namespace CMS.API.Tests.Infrastructure;

/// <summary>
/// End-to-end: every response the API sends carries the browser security headers, whichever component produced
/// it — the bearer handler's 401, a controller's 200, the exception middleware's 500 — and the Swagger UI gets the
/// one policy under which it can actually run.
/// </summary>
public class SecurityHeadersTests
{
    private const string ProtectedUrl = "/api/publish-statuses";
    private const string LoginUrl = "/api/auth/login";
    private const string SwaggerUiUrl = "/swagger/index.html";
    private const string SwaggerDocUrl = "/swagger/v1/swagger.json";

    private static HttpClient AdminClient(CmsApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CmsApiFactory.IssueToken(roles: [AuthorizationPolicies.AdminRole]));
        return client;
    }

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : throw new Xunit.Sdk.XunitException($"header {name} missing");

    private static void AssertApiHeaders(HttpResponseMessage response)
    {
        Assert.Equal(SecurityHeadersMiddleware.XContentTypeOptions, Header(response, HeaderNames.XContentTypeOptions));
        Assert.Equal(SecurityHeadersMiddleware.XFrameOptions, Header(response, HeaderNames.XFrameOptions));
        Assert.Equal(SecurityHeadersMiddleware.ReferrerPolicy, Header(response, SecurityHeadersMiddleware.ReferrerPolicyHeaderName));
        Assert.Equal(SecurityHeadersMiddleware.ContentSecurityPolicy, Header(response, HeaderNames.ContentSecurityPolicy));
        Assert.Equal(SecurityHeadersMiddleware.CacheControl, response.Headers.CacheControl?.ToString());
    }

    // ---- every kind of API response ----

    [Fact]
    public async Task Unauthenticated401_CarriesTheSecurityHeaders()
    {
        using var factory = new CmsApiFactory();

        var response = await factory.CreateClient().GetAsync(ProtectedUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertApiHeaders(response);
    }

    [Fact]
    public async Task Successful200_CarriesTheSecurityHeaders()
    {
        using var factory = new CmsApiFactory();

        var response = await AdminClient(factory).GetAsync(ProtectedUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertApiHeaders(response);
    }

    [Fact]
    public async Task UnhandledException500_CarriesTheSecurityHeaders()
    {
        using var factory = new CmsApiFactory();
        factory.PublishStatusRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var response = await AdminClient(factory).GetAsync(ProtectedUrl);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        AssertApiHeaders(response);
    }

    [Fact]
    public async Task LoginResponse_WhichContainsTheToken_IsNeverCacheable()
    {
        using var factory = new CmsApiFactory();

        var response = await factory.CreateClient().PostAsJsonAsync(LoginUrl,
            new LoginRequest { UserId = CmsApiFactory.UserId, Password = CmsApiFactory.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        AssertApiHeaders(response);
    }

    [Fact]
    public async Task ForbiddenAdminEndpoint403_CarriesTheSecurityHeaders()
    {
        using var factory = new CmsApiFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CmsApiFactory.IssueToken(roles: ["Editor"]));

        var response = await client.GetAsync("/api/app-users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        AssertApiHeaders(response);
    }

    // ---- HSTS only where a browser would honour it ----

    [Fact]
    public async Task OverPlainHttp_NoStrictTransportSecurity()
    {
        using var factory = new CmsApiFactory();

        var response = await factory.CreateClient().GetAsync(ProtectedUrl);

        Assert.False(response.Headers.Contains(HeaderNames.StrictTransportSecurity));
    }

    [Fact]
    public async Task OverHttps_StrictTransportSecurityIsSet()
    {
        using var factory = new CmsApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var response = await client.GetAsync(ProtectedUrl);

        Assert.Equal(SecurityHeadersMiddleware.StrictTransportSecurity, Header(response, HeaderNames.StrictTransportSecurity));
        AssertApiHeaders(response);
    }

    // ---- Swagger (the test host runs as Development, so it is mapped) ----

    [Fact]
    public async Task SwaggerUi_GetsTheRelaxedPolicy_AndIsNotMarkedNoStore()
    {
        using var factory = new CmsApiFactory();

        var response = await factory.CreateClient().GetAsync(SwaggerUiUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(SecurityHeadersMiddleware.SwaggerContentSecurityPolicy, Header(response, HeaderNames.ContentSecurityPolicy));
        Assert.Equal(SecurityHeadersMiddleware.XFrameOptions, Header(response, HeaderNames.XFrameOptions));
        Assert.Equal(SecurityHeadersMiddleware.XContentTypeOptions, Header(response, HeaderNames.XContentTypeOptions));
        Assert.Null(response.Headers.CacheControl);
    }

    [Fact]
    public async Task SwaggerDocument_GetsTheRelaxedPolicyToo()
    {
        using var factory = new CmsApiFactory();

        var response = await factory.CreateClient().GetAsync(SwaggerDocUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(SecurityHeadersMiddleware.SwaggerContentSecurityPolicy, Header(response, HeaderNames.ContentSecurityPolicy));
    }

    [Fact]
    public async Task ApiRouteWhoseNameMerelyStartsWithSwagger_IsNotRelaxed()
    {
        // "/swaggerish" is not under "/swagger": StartsWithSegments is segment-aware.
        using var factory = new CmsApiFactory();

        var response = await factory.CreateClient().GetAsync("/swaggerish");

        Assert.Equal(SecurityHeadersMiddleware.ContentSecurityPolicy, Header(response, HeaderNames.ContentSecurityPolicy));
    }

    // ---- the rule itself ----

    [Fact]
    public void Apply_DoesNotOverwriteAHeaderTheEndpointAlreadySet()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/anything";
        context.Response.Headers[HeaderNames.XFrameOptions] = "SAMEORIGIN";
        context.Response.Headers[HeaderNames.CacheControl] = "public, max-age=60";

        SecurityHeadersMiddleware.Apply(context);

        Assert.Equal("SAMEORIGIN", context.Response.Headers[HeaderNames.XFrameOptions]);
        Assert.Equal("public, max-age=60", context.Response.Headers[HeaderNames.CacheControl]);
        // The ones it did not touch are still added.
        Assert.Equal(SecurityHeadersMiddleware.XContentTypeOptions, context.Response.Headers[HeaderNames.XContentTypeOptions]);
        Assert.Equal(SecurityHeadersMiddleware.ContentSecurityPolicy, context.Response.Headers[HeaderNames.ContentSecurityPolicy]);
    }

    [Fact]
    public void Apply_Throws_OnNullContext()
    {
        Assert.Throws<ArgumentNullException>(() => SecurityHeadersMiddleware.Apply(null!));
    }
}
