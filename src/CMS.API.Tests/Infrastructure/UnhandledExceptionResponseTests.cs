using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CMS.API.Controllers;
using CMS.API.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace CMS.API.Tests.Infrastructure;

/// <summary>
/// End-to-end through the real pipeline: an exception escaping a controller / repository becomes one generic
/// 500 JSON body while the full exception lands in the server log — and the responses that already mean
/// something (401, 403, 400 validation, 409, the explicit AppConfig 500) are exactly what they were before.
/// <c>GET /api/publish-statuses</c> is the endpoint whose (mocked) repository is made to throw.
/// </summary>
public class UnhandledExceptionResponseTests
{
    private const string ThrowingUrl = "/api/publish-statuses";

    /// <summary>Deliberately looks like what SqlClient / Dapper would put in a message: SQL text and connection details.</summary>
    private const string LeakyMessage =
        "Invalid column name 'Secret'. SELECT PasswordHash FROM AppUser -- Server=.\\SQLEXPRESS;Database=CMS;Trusted_Connection=True";

    private static HttpClient AuthenticatedClient(CmsApiFactory factory, string? token = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token ?? CmsApiFactory.IssueToken(roles: ["Admin"]));
        return client;
    }

    private static CmsApiFactory FactoryWhoseRepositoryThrows(Exception exception)
    {
        var factory = new CmsApiFactory();
        factory.PublishStatusRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);
        return factory;
    }

    // ---- the unexpected path ----

    [Fact]
    public async Task AThrowingEndpoint_Returns500_WithOnlyTheGenericMessageAndATraceId()
    {
        using var factory = FactoryWhoseRepositoryThrows(new InvalidOperationException(LeakyMessage));
        var client = AuthenticatedClient(factory);

        var response = await client.GetAsync(ThrowingUrl);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal(["message", "traceId"], json.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(GlobalExceptionMiddleware.Message, json.RootElement.GetProperty("message").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task TheResponse_CarriesNoStackTraceSqlOrConnectionDetails()
    {
        using var factory = FactoryWhoseRepositoryThrows(new InvalidOperationException(LeakyMessage));
        var client = AuthenticatedClient(factory);

        var body = await (await client.GetAsync(ThrowingUrl)).Content.ReadAsStringAsync();

        Assert.DoesNotContain("SELECT", body);
        Assert.DoesNotContain("PasswordHash", body);
        Assert.DoesNotContain("SQLEXPRESS", body);
        Assert.DoesNotContain("Trusted_Connection", body);
        Assert.DoesNotContain("Invalid column name", body);
        Assert.DoesNotContain(nameof(InvalidOperationException), body);
        Assert.DoesNotContain("   at ", body);
        Assert.DoesNotContain(nameof(PublishStatusesController), body);
    }

    [Fact]
    public async Task TheFullException_IsLoggedServerSide_WithItsStackTrace()
    {
        var exception = new InvalidOperationException(LeakyMessage);
        using var factory = FactoryWhoseRepositoryThrows(exception);
        var client = AuthenticatedClient(factory);

        await client.GetAsync(ThrowingUrl);

        var entry = Assert.Single(factory.Logs.Entries,
            e => e.Level == LogLevel.Error && e.Category == typeof(GlobalExceptionMiddleware).FullName);
        Assert.Same(exception, entry.Exception);
        Assert.Contains("GET", entry.Message);
        Assert.Contains(ThrowingUrl, entry.Message);
        Assert.Contains(LeakyMessage, entry.Exception!.ToString());
        Assert.Contains(nameof(PublishStatusesController), entry.Exception.StackTrace);
    }

    [Fact]
    public async Task TheLoggedTraceId_MatchesTheOneInTheResponse()
    {
        using var factory = FactoryWhoseRepositoryThrows(new InvalidOperationException("boom"));
        var client = AuthenticatedClient(factory);

        var response = await client.GetAsync(ThrowingUrl);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var traceId = json.RootElement.GetProperty("traceId").GetString();
        Assert.Contains(factory.Logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains(traceId!));
    }

    [Fact]
    public async Task ANullReferenceInsideARepository_GetsTheSameGenericBody()
    {
        using var factory = FactoryWhoseRepositoryThrows(new NullReferenceException("Object reference not set"));
        var client = AuthenticatedClient(factory);

        var response = await client.GetAsync(ThrowingUrl);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UnexpectedErrorResponse>();
        Assert.Equal(GlobalExceptionMiddleware.Message, body!.Message);
    }

    // ---- responses that must stay exactly as they were ----

    [Fact]
    public async Task Unauthenticated_IsStill401_WithWwwAuthenticate_AndNoJsonBody()
    {
        using var factory = FactoryWhoseRepositoryThrows(new InvalidOperationException("never reached"));
        var client = factory.CreateClient();

        var response = await client.GetAsync(ThrowingUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
        factory.PublishStatusRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.DoesNotContain(factory.Logs.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task DefaultPasswordSession_IsStill403_WithItsOwnMessage()
    {
        using var factory = FactoryWhoseRepositoryThrows(new InvalidOperationException("never reached"));
        var client = AuthenticatedClient(factory, CmsApiFactory.IssueMustChangePasswordToken("Admin"));

        var response = await client.GetAsync(ThrowingUrl);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(PasswordChangeRequiredFilter.PasswordChangeRequiredMessage, json.RootElement.GetProperty("message").GetString());
        factory.PublishStatusRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ValidationFailure_IsStill400ValidationProblem_KeyedByField()
    {
        using var factory = new CmsApiFactory();
        var client = AuthenticatedClient(factory);

        var response = await client.PutAsJsonAsync("/api/auth/profile", new { userName = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("UserName", problem.Errors.Keys);
        Assert.DoesNotContain(factory.Logs.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task MissingQueryFilters_AreStill400_NotAGeneric500()
    {
        using var factory = new CmsApiFactory();
        var client = AuthenticatedClient(factory);

        var response = await client.GetAsync("/api/row-audits?tableName=Course");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("pkid", problem!.Errors.Keys);
    }

    [Fact]
    public async Task ADomainException_TheControllerMaps_IsStill409_NotAGeneric500()
    {
        using var factory = new CmsApiFactory();
        factory.PublishStatusRepository
            .Setup(r => r.DeleteAsync((byte)1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityInUseException("發布狀態仍被課程使用，無法刪除。"));
        var client = AuthenticatedClient(factory);

        var response = await client.DeleteAsync("/api/publish-statuses/1");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("發布狀態仍被課程使用，無法刪除。", json.RootElement.GetProperty("message").GetString());
        Assert.DoesNotContain(factory.Logs.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task ASuccessfulCall_IsUntouched()
    {
        using var factory = new CmsApiFactory();
        var client = AuthenticatedClient(factory);

        var response = await client.GetAsync(ThrowingUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(factory.Logs.Entries, e => e.Level == LogLevel.Error);
    }
}
