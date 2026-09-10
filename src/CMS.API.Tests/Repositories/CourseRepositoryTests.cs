using System.Data.Common;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace CMS.API.Tests.Repositories;

/// <summary>
/// The Course retrofit adds two things the Partner tests cannot show: the N-N junction lists are part of the
/// before / after snapshots (so a membership change is listed), and the JOINed label columns are not.
/// </summary>
public class CourseRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 1, 2, 3, TimeSpan.Zero);

    private readonly RecordingDbConnection _connection = new();
    private readonly CourseRepository _repository;

    public CourseRepositoryTests()
    {
        var writer = new RowAuditWriter(new HttpContextAccessor(), new FixedTimeProvider(Now));
        _repository = new CourseRepository(new RecordingConnectionFactory(_connection), writer);
    }

    private static Course Stored(int pkid = 1) => new()
    {
        Pkid = pkid,
        Title = "Azure Fundamentals",
        CourseId = "AZ-900",
        ProdCourseId = "AZ900",
        FriendlyUrl = "az-900",
        DisplayOrder = 1,
        PartnerPkid = 1,
        PublishStatusPkid = 1,
        ScheduleOn = new DateOnly(2026, 9, 1),
        ScheduleOff = new DateOnly(2026, 12, 31),
        Hour = 8,
        ListPrice = 12000m,
        LearningCredit = 8m,
        PartnerName = "Microsoft",
        PublishStatusDescription = "Published",
        CertificationPkids = [1, 2],
        JobCategoryPkids = [3]
    };

    private static CourseRequest Request(int pkid = 0) => new()
    {
        Pkid = pkid,
        Title = "Azure Fundamentals",
        CourseId = "AZ-900",
        ProdCourseId = "AZ900",
        FriendlyUrl = "az-900",
        DisplayOrder = 1,
        PartnerPkid = 1,
        PublishStatusPkid = 1,
        ScheduleOn = new DateOnly(2026, 9, 1),
        ScheduleOff = new DateOnly(2026, 12, 31),
        Hour = 8,
        ListPrice = 12000m,
        LearningCredit = 8m,
        CertificationPkids = [1, 2],
        JobCategoryPkids = [3]
    };

    /// <summary>One <c>GetByIdAsync</c> = the Course row, then its certification pkids, then its job-category pkids.</summary>
    private static IEnumerable<DbDataReader> Snapshot(Course course) =>
    [
        ListDataReader.Of(course),
        ListDataReader.Of(course.CertificationPkids.ToArray()),
        ListDataReader.Of(course.JobCategoryPkids.ToArray())
    ];

    private static Func<RecordedCommand, DbDataReader> InOrder(params IEnumerable<DbDataReader>[] snapshots)
    {
        var queue = new Queue<DbDataReader>(snapshots.SelectMany(s => s));
        return _ => queue.Dequeue();
    }

    [Fact]
    public async Task Create_WritesAnInsertAuditRow_WithTheTitle_AfterTheJunctionsAreSynced()
    {
        _connection.OnScalar = _ => 1;
        _connection.OnQuery = InOrder(Snapshot(Stored(1)));

        var pkid = await _repository.CreateAsync(Request(), CancellationToken.None);

        Assert.Equal(1, pkid);
        var audit = Assert.Single(_connection.AuditRows);
        Assert.Equal("Course", audit["TableName"]);
        Assert.Equal("1", audit["PrimaryKeyValues"]);
        Assert.Equal(RowAuditWriter.Insert, audit["ActionType"]);
        Assert.Equal("Azure Fundamentals", audit["ActionDesc"]);

        var lastJunctionInsert = _connection.Commands.Last(c => c.StartsWith("INSERT INTO CourseJobCategories"));
        Assert.True(_connection.Commands.IndexOf(lastJunctionInsert) < _connection.Commands.IndexOf(audit));
        Assert.Same(lastJunctionInsert.Transaction, audit.Transaction);
    }

    [Fact]
    public async Task Update_ListsChangedScalarsAndJunctionSets_ButNotJoinedLabelColumns()
    {
        var before = Stored(1);
        var after = Stored(1);
        after.PartnerPkid = 2;
        after.PartnerName = "Amazon";            // JOINed label: changes with PartnerPkid, must not be listed
        after.CertificationPkids = [1, 2, 3];    // membership grew
        _connection.OnQuery = InOrder(Snapshot(before), Snapshot(after));

        var request = Request(1);
        request.PartnerPkid = 2;
        request.CertificationPkids = [3, 1, 2];

        var result = await _repository.UpdateAsync(request, CancellationToken.None);

        Assert.True(result);
        var audit = Assert.Single(_connection.AuditRows);
        Assert.Equal(RowAuditWriter.Update, audit["ActionType"]);
        Assert.Equal("PartnerPkid, CertificationPkids", audit["ActionDesc"]);
    }

    [Fact]
    public async Task Update_WritesNoAuditRow_WhenOnlyTheJunctionOrderDiffers()
    {
        _connection.OnQuery = InOrder(Snapshot(Stored(1)), Snapshot(Stored(1)));
        var request = Request(1);
        request.CertificationPkids = [2, 1];

        await _repository.UpdateAsync(request, CancellationToken.None);

        Assert.Empty(_connection.AuditRows);
    }

    [Fact]
    public async Task Delete_WritesADeleteAuditRow_WithTheTitle()
    {
        _connection.OnQuery = InOrder(Snapshot(Stored(1)));

        var result = await _repository.DeleteAsync(1, CancellationToken.None);

        Assert.True(result);
        var audit = Assert.Single(_connection.AuditRows);
        Assert.Equal(RowAuditWriter.Delete, audit["ActionType"]);
        Assert.Equal("1", audit["PrimaryKeyValues"]);
        Assert.Equal("Azure Fundamentals", audit["ActionDesc"]);
    }
}
