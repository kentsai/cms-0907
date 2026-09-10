using System.Data.Common;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace CMS.API.Tests.Repositories;

/// <summary>
/// Proves the generic RowAudit wiring on one Lab 03 repository: every successful INSERT / UPDATE / DELETE writes
/// exactly one audit row on the change's own transaction, with the pkid, the first string column (Name) or the
/// changed-column list as description, and a change that does not happen leaves no audit row behind. SELECTs are
/// answered by <see cref="RecordingDbConnection.OnQuery"/>, so no SQL Server is involved.
/// </summary>
public class PartnerRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 1, 2, 3, TimeSpan.Zero);

    private readonly RecordingDbConnection _connection = new();
    private readonly PartnerRepository _repository;

    public PartnerRepositoryTests()
    {
        // No HttpContext → UserName "system"; the user-name resolution itself is covered by RowAuditWriterTests.
        var writer = new RowAuditWriter(new HttpContextAccessor(), new FixedTimeProvider(Now));
        _repository = new PartnerRepository(new RecordingConnectionFactory(_connection), writer);
    }

    private static Partner Stored(short pkid = 7) => new()
    {
        Pkid = pkid,
        Name = "Microsoft",
        AppKey = "MS",
        NameOnPartnerMenu = "Microsoft 微軟",
        NameOnCourseDetailPage = "微軟",
        DisplayOrder = 10,
        ImageFilename = "microsoft.png"
    };

    private static PartnerRequest Request(short pkid = 0) => new()
    {
        Pkid = pkid,
        Name = "Microsoft",
        AppKey = "MS",
        NameOnPartnerMenu = "Microsoft 微軟",
        NameOnCourseDetailPage = "微軟",
        DisplayOrder = 10,
        ImageFilename = "microsoft.png"
    };

    /// <summary>Answers successive SELECTs with the given readers in order (before-image, then after-image).</summary>
    private static Func<RecordedCommand, DbDataReader> InOrder(params DbDataReader[] readers)
    {
        var queue = new Queue<DbDataReader>(readers);
        return _ => queue.Dequeue();
    }

    private RecordedCommand Statement(string prefix) => Assert.Single(_connection.Commands, c => c.StartsWith(prefix));

    private int IndexOf(RecordedCommand command) => _connection.Commands.IndexOf(command);

    // ---- INSERT ----

    [Fact]
    public async Task Create_WritesOneInsertAuditRow_WithTheNewPkidAndTheName()
    {
        _connection.OnScalar = _ => (short)7;
        _connection.OnQuery = _ => ListDataReader.Of(Stored(7));

        var pkid = await _repository.CreateAsync(Request(), CancellationToken.None);

        Assert.Equal(7, pkid);
        var audit = Assert.Single(_connection.AuditRows);
        Assert.Equal("Partner", audit["TableName"]);
        Assert.Equal("7", audit["PrimaryKeyValues"]);
        Assert.Equal(RowAuditWriter.Insert, audit["ActionType"]);
        Assert.Equal("Microsoft", audit["ActionDesc"]);
        Assert.Equal(RowAuditWriter.SystemUserName, audit["UserName"]);
    }

    [Fact]
    public async Task Create_AuditsAfterTheInsert_OnTheSameTransaction()
    {
        _connection.OnScalar = _ => (short)7;
        _connection.OnQuery = _ => ListDataReader.Of(Stored(7));

        await _repository.CreateAsync(Request(), CancellationToken.None);

        var insert = Statement("INSERT INTO Partner");
        var audit = Assert.Single(_connection.AuditRows);
        Assert.NotNull(insert.Transaction);
        Assert.Same(insert.Transaction, audit.Transaction);
        Assert.True(IndexOf(audit) > IndexOf(insert), "the audit row must follow the data change");
    }

    [Fact]
    public async Task Create_LeavesNoAuditRow_WhenTheInsertFails()
    {
        _connection.OnScalar = _ => throw new InvalidOperationException("duplicate AppKey");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.CreateAsync(Request(), CancellationToken.None));

        Assert.Empty(_connection.AuditRows);
        Assert.Single(_connection.Commands); // only the failed INSERT ran
    }

    // ---- UPDATE ----

    [Fact]
    public async Task Update_WritesOneUpdateAuditRow_ListingExactlyTheChangedColumns()
    {
        var before = Stored(7);
        var after = Stored(7);
        after.Name = "Microsoft Taiwan";
        after.DisplayOrder = 20;
        after.ImageFilename = null;
        _connection.OnQuery = InOrder(ListDataReader.Of(before), ListDataReader.Of(after));

        var request = Request(7);
        request.Name = "Microsoft Taiwan";
        request.DisplayOrder = 20;
        request.ImageFilename = null;

        var result = await _repository.UpdateAsync(request, CancellationToken.None);

        Assert.True(result);
        var audit = Assert.Single(_connection.AuditRows);
        Assert.Equal("Partner", audit["TableName"]);
        Assert.Equal("7", audit["PrimaryKeyValues"]);
        Assert.Equal(RowAuditWriter.Update, audit["ActionType"]);
        Assert.Equal("Name, DisplayOrder, ImageFilename", audit["ActionDesc"]);
    }

    [Fact]
    public async Task Update_LoadsTheBeforeImage_ThenUpdates_ThenAuditsOnTheSameTransaction()
    {
        var after = Stored(7);
        after.Name = "Microsoft Taiwan";
        _connection.OnQuery = InOrder(ListDataReader.Of(Stored(7)), ListDataReader.Of(after));
        var request = Request(7);
        request.Name = "Microsoft Taiwan";

        await _repository.UpdateAsync(request, CancellationToken.None);

        var update = Statement("UPDATE Partner");
        var audit = Assert.Single(_connection.AuditRows);
        var firstSelect = _connection.Commands.First(c => c.StartsWith("SELECT"));
        Assert.True(IndexOf(firstSelect) < IndexOf(update), "the before-image is read before the UPDATE");
        Assert.True(IndexOf(update) < IndexOf(audit), "the audit row follows the UPDATE");
        Assert.NotNull(update.Transaction);
        Assert.Same(update.Transaction, audit.Transaction);
        Assert.Equal("Microsoft Taiwan", update["Name"]);
    }

    [Fact]
    public async Task Update_WritesNoAuditRow_WhenTheSaveChangedNothing()
    {
        _connection.OnQuery = InOrder(ListDataReader.Of(Stored(7)), ListDataReader.Of(Stored(7)));

        var result = await _repository.UpdateAsync(Request(7), CancellationToken.None);

        Assert.True(result);
        Statement("UPDATE Partner"); // the UPDATE itself still ran
        Assert.Empty(_connection.AuditRows);
    }

    [Fact]
    public async Task Update_ReturnsFalseAndWritesNothing_WhenTheRowDoesNotExist()
    {
        _connection.OnQuery = _ => ListDataReader.Of<Partner>();

        var result = await _repository.UpdateAsync(Request(404), CancellationToken.None);

        Assert.False(result);
        Assert.DoesNotContain(_connection.Commands, c => c.StartsWith("UPDATE"));
        Assert.Empty(_connection.AuditRows);
    }

    [Fact]
    public async Task Update_LeavesNoAuditRow_WhenTheUpdateStatementFails()
    {
        _connection.OnQuery = _ => ListDataReader.Of(Stored(7));
        _connection.OnExecute = command => command.StartsWith("UPDATE Partner")
            ? throw new InvalidOperationException("deadlock victim")
            : 1;

        await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.UpdateAsync(Request(7), CancellationToken.None));

        Statement("UPDATE Partner"); // it was attempted...
        Assert.Empty(_connection.AuditRows); // ...but nothing was audited
    }

    [Fact]
    public async Task Update_LeavesNoAuditRow_WhenTheUpdateAffectedNoRow()
    {
        _connection.OnQuery = _ => ListDataReader.Of(Stored(7));
        _connection.OnExecute = command => command.StartsWith("UPDATE Partner") ? 0 : 1;

        var result = await _repository.UpdateAsync(Request(7), CancellationToken.None);

        Assert.False(result);
        Assert.Empty(_connection.AuditRows);
    }

    // ---- DELETE ----

    [Fact]
    public async Task Delete_WritesOneDeleteAuditRow_WithThePkidAndTheDeletedName()
    {
        _connection.OnQuery = _ => ListDataReader.Of(Stored(7));

        var result = await _repository.DeleteAsync(7, CancellationToken.None);

        Assert.True(result);
        var audit = Assert.Single(_connection.AuditRows);
        Assert.Equal("Partner", audit["TableName"]);
        Assert.Equal("7", audit["PrimaryKeyValues"]);
        Assert.Equal(RowAuditWriter.Delete, audit["ActionType"]);
        Assert.Equal("Microsoft", audit["ActionDesc"]);
    }

    [Fact]
    public async Task Delete_LoadsTheRowFirst_ThenAuditsAfterTheDelete_OnTheSameTransaction()
    {
        _connection.OnQuery = _ => ListDataReader.Of(Stored(7));

        await _repository.DeleteAsync(7, CancellationToken.None);

        var select = _connection.Commands.First(c => c.StartsWith("SELECT"));
        var delete = Statement("DELETE FROM Partner");
        var audit = Assert.Single(_connection.AuditRows);
        Assert.True(IndexOf(select) < IndexOf(delete) && IndexOf(delete) < IndexOf(audit));
        Assert.NotNull(delete.Transaction);
        Assert.Same(delete.Transaction, audit.Transaction);
    }

    [Fact]
    public async Task Delete_ReturnsFalseAndWritesNothing_WhenTheRowDoesNotExist()
    {
        _connection.OnQuery = _ => ListDataReader.Of<Partner>();

        var result = await _repository.DeleteAsync(404, CancellationToken.None);

        Assert.False(result);
        Assert.DoesNotContain(_connection.Commands, c => c.StartsWith("DELETE"));
        Assert.Empty(_connection.AuditRows);
    }

    [Fact]
    public async Task Delete_LeavesNoAuditRow_WhenTheDeleteStatementFails()
    {
        _connection.OnQuery = _ => ListDataReader.Of(Stored(7));
        _connection.OnExecute = command => command.StartsWith("DELETE FROM Partner")
            ? throw new InvalidOperationException("connection lost")
            : 1;

        await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.DeleteAsync(7, CancellationToken.None));

        Assert.Empty(_connection.AuditRows);
    }
}
