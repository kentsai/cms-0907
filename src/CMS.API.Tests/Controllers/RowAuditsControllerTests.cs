using System.Security.Claims;
using CMS.API.Controllers;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests.Controllers;

public class RowAuditsControllerTests
{
    private readonly Mock<IRowAuditRepository> _repository = new(MockBehavior.Strict);
    private readonly RowAuditsController _controller;

    public RowAuditsControllerTests()
    {
        _controller = new RowAuditsController(_repository.Object);
        SignIn(_controller, AuthorizationPolicies.AdminRole);
    }

    /// <summary>
    /// Puts a principal on the controller. The AppUser / AppRole trails are administrator-only, so these
    /// tests have to say which kind of caller they are; pass no roles for a signed-in non-administrator.
    /// </summary>
    private static void SignIn(RowAuditsController controller, params string[] roles)
    {
        var identity = new ClaimsIdentity(
            roles.Select(role => new Claim(ClaimTypes.Role, role)), authenticationType: "Test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private static RowAudit Entry(int daysAgo, string actionType = "UPDATE", string? desc = "Title") => new()
    {
        DateTime = new DateTime(2026, 9, 8, 6, 30, 0, DateTimeKind.Unspecified).AddDays(-daysAgo),
        UserName = "alice",
        ActionType = actionType,
        ActionDesc = desc
    };

    // ---- GET /api/row-audits?tableName=&pkid= ----

    [Fact]
    public async Task GetForRecord_FiltersByTableNameAndPkid_AndReturnsTheRepositoryRowsInOrder()
    {
        var expected = new List<RowAudit> { Entry(0), Entry(1, "INSERT", "Azure Fundamentals") };
        _repository
            .Setup(r => r.GetForRecordAsync("Course", "123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _controller.GetForRecord("Course", "123", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var rows = Assert.IsAssignableFrom<IReadOnlyList<RowAudit>>(ok.Value);
        Assert.Equal(expected, rows); // same instances, same (newest-first) order the repository produced
        _repository.Verify(r => r.GetForRecordAsync("Course", "123", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetForRecord_TrimsTheFilters_BeforeQuerying()
    {
        _repository
            .Setup(r => r.GetForRecordAsync("AppRole", "Admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _controller.GetForRecord(" AppRole ", " Admin ", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        _repository.Verify(r => r.GetForRecordAsync("AppRole", "Admin", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- The account tables' audit trail is administrator-only ----
    //
    // `tableName` arrives from the query string, so without this check the Admin policy on
    // AppUsersController / AppRolesController guarded the rows but not their history: any signed-in
    // caller could ask who changed which account and when. That trail also used to name a password
    // reset to the system default, which turned it into a list of accounts anyone could log in as.

    [Theory]
    [InlineData("AppUser", "helen")]
    [InlineData("AppRole", "Admin")]
    [InlineData("appuser", "helen")]  // caller-supplied, so the match is case-insensitive
    [InlineData(" AppRole ", "Admin")] // and applies after trimming
    public async Task GetForRecord_Returns403_ForAnAccountTable_WhenTheCallerIsNotAnAdmin_AndNeverQueries(
        string tableName, string pkid)
    {
        var controller = new RowAuditsController(_repository.Object);
        SignIn(controller); // signed in, no Admin role

        var result = await controller.GetForRecord(tableName, pkid, CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        _repository.Verify(
            r => r.GetForRecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("AppUser", "helen")]
    [InlineData("AppRole", "Admin")]
    public async Task GetForRecord_ReturnsTheTrail_ForAnAccountTable_WhenTheCallerIsAnAdmin(
        string tableName, string pkid)
    {
        _repository
            .Setup(r => r.GetForRecordAsync(tableName, pkid, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Entry(0)]);

        var result = await _controller.GetForRecord(tableName, pkid, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Theory]
    [InlineData("Course", "123")]
    [InlineData("Partner", "7")]
    [InlineData("PublishStatus", "1")]
    public async Task GetForRecord_StaysOpenToEverySignedInUser_ForTheContentTables(string tableName, string pkid)
    {
        var controller = new RowAuditsController(_repository.Object);
        SignIn(controller); // signed in, no Admin role
        _repository
            .Setup(r => r.GetForRecordAsync(tableName, pkid, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Entry(0)]);

        var result = await controller.GetForRecord(tableName, pkid, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetForRecord_ReturnsEmptyList_WhenTheRecordHasNoHistory()
    {
        _repository
            .Setup(r => r.GetForRecordAsync("Partner", "7", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _controller.GetForRecord("Partner", "7", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<RowAudit>>(ok.Value));
    }

    [Theory]
    [InlineData(null, "123", "tableName")]
    [InlineData("", "123", "tableName")]
    [InlineData("   ", "123", "tableName")]
    [InlineData("Course", null, "pkid")]
    [InlineData("Course", "", "pkid")]
    [InlineData("Course", "  ", "pkid")]
    public async Task GetForRecord_Returns400_WhenAFilterIsMissing_AndNeverQueries(string? tableName, string? pkid, string field)
    {
        var result = await _controller.GetForRecord(tableName, pkid, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, bad.StatusCode);
        var problem = Assert.IsType<ValidationProblemDetails>(bad.Value);
        Assert.Contains(field, problem.Errors.Keys);
        _repository.Verify(r => r.GetForRecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetForRecord_Returns400ListingBothFields_WhenBothAreMissing()
    {
        var result = await _controller.GetForRecord(null, null, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problem = Assert.IsType<ValidationProblemDetails>(bad.Value);
        Assert.Equal(["pkid", "tableName"], problem.Errors.Keys.OrderBy(k => k, StringComparer.Ordinal));
    }
}
