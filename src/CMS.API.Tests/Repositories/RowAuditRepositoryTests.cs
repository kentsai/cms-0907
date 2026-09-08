using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Tests.Infrastructure;

namespace CMS.API.Tests.Repositories;

/// <summary>
/// The read side of RowAudit on <see cref="RecordingDbConnection"/>: one SELECT filtered by both
/// <c>TableName</c> and <c>PrimaryKeyValues</c>, ordered newest first, mapped to the four badge columns.
/// </summary>
public class RowAuditRepositoryTests
{
    private readonly RecordingDbConnection _connection = new();
    private readonly RowAuditRepository _repository;

    public RowAuditRepositoryTests()
    {
        _repository = new RowAuditRepository(new RecordingConnectionFactory(_connection));
    }

    private static RowAudit Entry(string userName, int hour, string actionType = "UPDATE", string? desc = "Title") => new()
    {
        DateTime = new DateTime(2026, 9, 8, hour, 0, 0),
        UserName = userName,
        ActionType = actionType,
        ActionDesc = desc
    };

    [Fact]
    public async Task GetForRecord_SelectsWithBothFiltersAsParameters()
    {
        _connection.OnQuery = _ => ListDataReader.Of<RowAudit>();

        await _repository.GetForRecordAsync("Course", "123", CancellationToken.None);

        var select = Assert.Single(_connection.Commands);
        Assert.Contains("FROM RowAudit", select.CommandText);
        Assert.Contains("WHERE TableName = @TableName AND PrimaryKeyValues = @Pkid", select.CommandText);
        Assert.Equal("Course", select["TableName"]);
        Assert.Equal("123", select["Pkid"]);
        Assert.Null(select.Transaction);
    }

    [Fact]
    public async Task GetForRecord_PassesAStringKeyThroughUnchanged()
    {
        _connection.OnQuery = _ => ListDataReader.Of<RowAudit>();

        await _repository.GetForRecordAsync("AppRole", "Admin", CancellationToken.None);

        var select = Assert.Single(_connection.Commands);
        Assert.Equal("AppRole", select["TableName"]);
        Assert.Equal("Admin", select["Pkid"]);
    }

    [Fact]
    public void Sql_OrdersNewestFirst_WithTheAuditRowIdentityAsTieBreaker()
    {
        Assert.Contains("ORDER BY [DateTime] DESC, pkid DESC", RowAuditRepository.Sql);
        Assert.StartsWith("SELECT [DateTime], UserName, ActionType, ActionDesc", RowAuditRepository.Sql.TrimStart());
    }

    [Fact]
    public async Task GetForRecord_MapsEveryRowInTheOrderTheDatabaseReturnedIt()
    {
        // The reader is the database's answer to ORDER BY ... DESC: newest first. The repository must not re-sort it.
        var newest = Entry("alice", 14);
        var middle = Entry("bob", 9, "UPDATE", "Title, ScheduleOn");
        var oldest = Entry("system", 8, "INSERT", "Azure Fundamentals");
        _connection.OnQuery = _ => ListDataReader.Of(newest, middle, oldest);

        var rows = await _repository.GetForRecordAsync("Course", "123", CancellationToken.None);

        Assert.Equal(3, rows.Count);
        Assert.Equal(["alice", "bob", "system"], rows.Select(r => r.UserName));
        Assert.Equal(["UPDATE", "UPDATE", "INSERT"], rows.Select(r => r.ActionType));
        Assert.Equal(new DateTime(2026, 9, 8, 14, 0, 0), rows[0].DateTime);
        Assert.Equal("Azure Fundamentals", rows[2].ActionDesc);
        Assert.True(rows.Zip(rows.Skip(1)).All(pair => pair.First.DateTime >= pair.Second.DateTime));
    }

    [Fact]
    public async Task GetForRecord_ReturnsAnEmptyList_WhenTheRecordHasNoHistory()
    {
        _connection.OnQuery = _ => ListDataReader.Of<RowAudit>();

        var rows = await _repository.GetForRecordAsync("Partner", "7", CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task GetForRecord_ReadsANullActionDesc()
    {
        _connection.OnQuery = _ => ListDataReader.Of(Entry("alice", 10, "INSERT", desc: null));

        var rows = await _repository.GetForRecordAsync("Certification", "5", CancellationToken.None);

        Assert.Null(Assert.Single(rows).ActionDesc);
    }
}
