using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CMS.API.Models;
using Moq;

namespace CMS.API.Tests.Infrastructure;

/// <summary>
/// End-to-end over the real pipeline: <c>GET /api/row-audits?tableName=&amp;pkid=</c> binds both query-string
/// filters, needs a bearer token like every other endpoint, and serialises the repository's newest-first rows
/// with exactly the four badge fields.
/// </summary>
public class RowAuditsEndpointTests
{
    private const string Url = "/api/row-audits";

    private static HttpClient AuthenticatedClient(CmsApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CmsApiFactory.IssueToken(roles: ["Admin"]));
        return client;
    }

    private static RowAudit Entry(DateTime at, string userName, string actionType, string? desc) => new()
    {
        DateTime = at,
        UserName = userName,
        ActionType = actionType,
        ActionDesc = desc
    };

    [Fact]
    public async Task Get_FiltersByTableNameAndPkid_AndReturnsRowsNewestFirst()
    {
        using var factory = new CmsApiFactory();
        var newest = Entry(new DateTime(2026, 9, 8, 14, 30, 0), "alice", "UPDATE", "Title");
        var older = Entry(new DateTime(2026, 9, 7, 9, 0, 0), "bob", "UPDATE", "ScheduleOn, ScheduleOff");
        var oldest = Entry(new DateTime(2026, 9, 1, 8, 0, 0), "system", "INSERT", "Azure Fundamentals");
        factory.RowAuditRepository
            .Setup(r => r.GetForRecordAsync("Course", "123", It.IsAny<CancellationToken>()))
            .ReturnsAsync([newest, older, oldest]);
        var client = AuthenticatedClient(factory);

        var response = await client.GetAsync($"{Url}?tableName=Course&pkid=123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = await response.Content.ReadFromJsonAsync<List<RowAudit>>();
        Assert.NotNull(rows);
        Assert.Equal(["alice", "bob", "system"], rows.Select(r => r.UserName));
        Assert.Equal(["UPDATE", "UPDATE", "INSERT"], rows.Select(r => r.ActionType));
        Assert.Equal(["Title", "ScheduleOn, ScheduleOff", "Azure Fundamentals"], rows.Select(r => r.ActionDesc));
        Assert.Equal(newest.DateTime, rows[0].DateTime);
        Assert.True(rows.Zip(rows.Skip(1)).All(pair => pair.First.DateTime > pair.Second.DateTime), "rows must be newest first");
        factory.RowAuditRepository.Verify(r => r.GetForRecordAsync("Course", "123", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Get_SerialisesExactlyTheFourBadgeFields()
    {
        using var factory = new CmsApiFactory();
        factory.RowAuditRepository
            .Setup(r => r.GetForRecordAsync("Partner", "7", It.IsAny<CancellationToken>()))
            .ReturnsAsync([Entry(new DateTime(2026, 9, 8, 1, 2, 3), "alice", "DELETE", "Microsoft")]);
        var client = AuthenticatedClient(factory);

        using var document = JsonDocument.Parse(await client.GetStringAsync($"{Url}?tableName=Partner&pkid=7"));

        var row = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal(["dateTime", "userName", "actionType", "actionDesc"], row.EnumerateObject().Select(p => p.Name));
        Assert.Equal("2026-09-08T01:02:03", row.GetProperty("dateTime").GetString());
        Assert.Equal("alice", row.GetProperty("userName").GetString());
        Assert.Equal("DELETE", row.GetProperty("actionType").GetString());
        Assert.Equal("Microsoft", row.GetProperty("actionDesc").GetString());
    }

    [Fact]
    public async Task Get_PassesAStringKeyThrough_ForAppRoleAndAppUser()
    {
        using var factory = new CmsApiFactory();
        factory.RowAuditRepository
            .Setup(r => r.GetForRecordAsync("AppUser", "helen", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var client = AuthenticatedClient(factory);

        var response = await client.GetAsync($"{Url}?tableName=AppUser&pkid=helen");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Get_ReturnsEmptyArray_ForARecordWithNoHistory()
    {
        using var factory = new CmsApiFactory();
        factory.RowAuditRepository
            .Setup(r => r.GetForRecordAsync("Course", "999", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var client = AuthenticatedClient(factory);

        var rows = await client.GetFromJsonAsync<List<RowAudit>>($"{Url}?tableName=Course&pkid=999");

        Assert.NotNull(rows);
        Assert.Empty(rows);
    }

    [Theory]
    [InlineData("?pkid=123")]
    [InlineData("?tableName=Course")]
    [InlineData("?tableName=&pkid=123")]
    [InlineData("")]
    public async Task Get_Returns400_WhenAFilterIsMissing_WithoutQuerying(string query)
    {
        using var factory = new CmsApiFactory();
        var client = AuthenticatedClient(factory);

        var response = await client.GetAsync(Url + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        factory.RowAuditRepository.Verify(
            r => r.GetForRecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Get_Returns401_WithoutABearerToken()
    {
        using var factory = new CmsApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"{Url}?tableName=Course&pkid=123");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        factory.RowAuditRepository.Verify(
            r => r.GetForRecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
