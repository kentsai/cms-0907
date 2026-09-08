using CMS.API.Controllers;
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
