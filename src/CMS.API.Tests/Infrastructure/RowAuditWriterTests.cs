using System.Security.Claims;
using CMS.API.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace CMS.API.Tests.Infrastructure;

/// <summary>
/// The generic Log* methods of <see cref="RowAuditWriter"/> are asserted through a <see cref="RecordingDbConnection"/>,
/// so every test checks the parameter values that would reach <c>INSERT INTO RowAudit</c> (no SQL Server needed).
/// </summary>
public class RowAuditWriterTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 1, 2, 3, TimeSpan.Zero);

    /// <summary>Declaration order matters: a non-string first, then two strings, then a list.</summary>
    private sealed class Widget
    {
        public int Pkid { get; set; }
        public bool IsActive { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public decimal Price { get; set; }
        public List<int> TagPkids { get; set; } = [];
    }

    private sealed class Counter
    {
        public short pkid { get; set; }
        public int Count { get; set; }
    }

    private sealed class Keyless
    {
        public string RoleId { get; set; } = string.Empty;
    }

    /// <summary>Shape of AppRole / AppUser: a surrogate pkid, a string [AuditKey] and an [AuditIgnore]d label.</summary>
    private sealed class StringKeyed
    {
        public int Pkid { get; set; }
        [AuditKey] public string RoleId { get; set; } = string.Empty;
        [AuditIgnore] public string Label { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
    }

    private static Widget SampleWidget() => new()
    {
        Pkid = 7, IsActive = true, Code = "W-007", Name = "Gadget", Price = 9.5m, TagPkids = [1, 2]
    };

    private static RowAuditWriter CreateWriter(ClaimsPrincipal? user, DateTimeOffset? now = null)
    {
        var accessor = new HttpContextAccessor();
        if (user is not null)
        {
            accessor.HttpContext = new DefaultHttpContext { User = user };
        }

        return new RowAuditWriter(accessor, new FixedTimeProvider(now ?? Now));
    }

    private static ClaimsPrincipal AuthenticatedUser(string? userName, string userId = "u001")
    {
        var claims = new List<Claim> { new(JwtTokenIssuer.UserIdClaim, userId) };
        if (userName is not null)
        {
            claims.Add(new Claim(JwtTokenIssuer.UserNameClaim, userName));
        }

        // Same claim mapping as ConfigureJwtBearerOptions: Identity.Name is the userId claim.
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test", JwtTokenIssuer.UserIdClaim, ClaimTypes.Role));
    }

    private static ClaimsPrincipal Signed(string userName = "Ken Tsai") => AuthenticatedUser(userName);

    // ---- Insert / Delete: pkid + first string property ----

    [Fact]
    public async Task LogInsert_WritesPkidAndFirstStringPropertyAsActionDesc()
    {
        var connection = new RecordingDbConnection();
        var writer = CreateWriter(Signed());

        await writer.LogInsertAsync(connection, "Widget", SampleWidget(), CancellationToken.None);

        var command = Assert.Single(connection.Commands);
        Assert.Equal("Widget", command["TableName"]);
        Assert.Equal("7", command["PrimaryKeyValues"]);
        Assert.Equal(RowAuditWriter.Insert, command["ActionType"]);
        Assert.Equal("W-007", command["ActionDesc"]);
    }

    [Fact]
    public async Task LogDelete_WritesPkidAndFirstStringPropertyAsActionDesc()
    {
        var connection = new RecordingDbConnection();
        var writer = CreateWriter(Signed());

        await writer.LogDeleteAsync(connection, "Widget", SampleWidget(), CancellationToken.None);

        var command = Assert.Single(connection.Commands);
        Assert.Equal("7", command["PrimaryKeyValues"]);
        Assert.Equal(RowAuditWriter.Delete, command["ActionType"]);
        Assert.Equal("W-007", command["ActionDesc"]);
    }

    [Fact]
    public async Task LogInsert_ActionDescIsNull_WhenEntityHasNoStringProperty()
    {
        var connection = new RecordingDbConnection();
        var writer = CreateWriter(Signed());

        await writer.LogInsertAsync(connection, "Counter", new Counter { pkid = 3, Count = 1 }, CancellationToken.None);

        var command = Assert.Single(connection.Commands);
        Assert.Null(command["ActionDesc"]);
    }

    [Fact]
    public async Task Insert_DoesNotWriteTheIdentityColumn()
    {
        var connection = new RecordingDbConnection();
        var writer = CreateWriter(Signed());

        await writer.LogInsertAsync(connection, "Widget", SampleWidget(), CancellationToken.None);

        var command = Assert.Single(connection.Commands);
        Assert.Contains("INSERT INTO RowAudit", command.CommandText);
        Assert.DoesNotContain("pkid", command.CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            new[] { "ActionDesc", "ActionType", "DateTime", "PrimaryKeyValues", "TableName", "UserName" },
            command.Parameters.Keys.Order().ToArray());
    }

    // ---- Update: changed property names ----

    [Fact]
    public async Task LogUpdate_ListsExactlyTheChangedPropertyNames()
    {
        var connection = new RecordingDbConnection();
        var writer = CreateWriter(Signed());
        var before = SampleWidget();
        var after = SampleWidget();
        after.Name = "Gizmo";
        after.Price = 10m;
        after.TagPkids = [1, 2, 3];

        await writer.LogUpdateAsync(connection, "Widget", before, after, CancellationToken.None);

        var command = Assert.Single(connection.Commands);
        Assert.Equal(RowAuditWriter.Update, command["ActionType"]);
        Assert.Equal("7", command["PrimaryKeyValues"]);
        Assert.Equal("Name, Price, TagPkids", command["ActionDesc"]);
    }

    [Fact]
    public async Task LogUpdate_SkipsTheRow_WhenNothingChanged()
    {
        var connection = new RecordingDbConnection();
        var writer = CreateWriter(Signed());

        await writer.LogUpdateAsync(connection, "Widget", SampleWidget(), SampleWidget(), CancellationToken.None);

        Assert.Empty(connection.Commands);
    }

    [Fact]
    public async Task LogUpdate_TreatsNullToValueAsAChange()
    {
        var connection = new RecordingDbConnection();
        var writer = CreateWriter(Signed());
        var before = SampleWidget();
        before.Name = null;

        await writer.LogUpdateAsync(connection, "Widget", before, SampleWidget(), CancellationToken.None);

        Assert.Equal("Name", Assert.Single(connection.Commands)["ActionDesc"]);
    }

    // ---- PrimaryKeyValues ----

    [Fact]
    public void PrimaryKeyValues_ReadsThePkidPropertyCaseInsensitively()
    {
        Assert.Equal("7", RowAuditWriter.PrimaryKeyValues(SampleWidget()));
        Assert.Equal("12", RowAuditWriter.PrimaryKeyValues(new Counter { pkid = 12 }));
    }

    [Fact]
    public void PrimaryKeyValues_Throws_WhenEntityHasNoPkid()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => RowAuditWriter.PrimaryKeyValues(new Keyless { RoleId = "Admin" }));

        Assert.Contains(nameof(Keyless), ex.Message);
        Assert.Contains("pkid", ex.Message);
    }

    [Fact]
    public void FirstStringValue_SkipsNonStringPropertiesDeclaredEarlier()
    {
        Assert.Equal("W-007", RowAuditWriter.FirstStringValue(SampleWidget()));
        Assert.Null(RowAuditWriter.FirstStringValue(new Counter()));
    }

    // ---- [AuditKey] / [AuditIgnore] ----

    [Fact]
    public async Task StringKeyedEntity_UsesTheAuditKeyAsPrimaryKeyValues_AndSkipsItAndIgnoredMembersInActionDesc()
    {
        var connection = new RecordingDbConnection();
        var writer = CreateWriter(Signed());
        var role = new StringKeyed { Pkid = 5, RoleId = "Admin", Label = "5 users", RoleName = "Administrator" };

        await writer.LogInsertAsync(connection, "AppRole", role, CancellationToken.None);

        var command = Assert.Single(connection.Commands);
        Assert.Equal("Admin", command["PrimaryKeyValues"]);
        Assert.Equal("Administrator", command["ActionDesc"]);
    }

    [Fact]
    public async Task LogUpdate_DoesNotListAuditIgnoredMembers()
    {
        var connection = new RecordingDbConnection();
        var writer = CreateWriter(Signed());
        var before = new StringKeyed { Pkid = 5, RoleId = "Admin", Label = "5 users", RoleName = "Administrator" };
        var onlyLabelChanged = new StringKeyed { Pkid = 5, RoleId = "Admin", Label = "6 users", RoleName = "Administrator" };
        var nameChanged = new StringKeyed { Pkid = 5, RoleId = "Admin", Label = "6 users", RoleName = "Admins" };

        await writer.LogUpdateAsync(connection, "AppRole", before, onlyLabelChanged, CancellationToken.None);
        Assert.Empty(connection.Commands);

        await writer.LogUpdateAsync(connection, "AppRole", before, nameChanged, CancellationToken.None);
        var command = Assert.Single(connection.Commands);
        Assert.Equal("Admin", command["PrimaryKeyValues"]);
        Assert.Equal("RoleName", command["ActionDesc"]);
    }

    // ---- UserName ----

    [Fact]
    public async Task UserName_IsSystem_WhenThereIsNoHttpContext()
    {
        var connection = new RecordingDbConnection();
        var writer = CreateWriter(user: null);

        await writer.LogInsertAsync(connection, "Widget", SampleWidget(), CancellationToken.None);

        Assert.Equal(RowAuditWriter.SystemUserName, Assert.Single(connection.Commands)["UserName"]);
    }

    [Fact]
    public async Task UserName_IsSystem_WhenTheUserIsNotAuthenticated()
    {
        var connection = new RecordingDbConnection();
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(JwtTokenIssuer.UserNameClaim, "Nobody")], authenticationType: null));
        var writer = CreateWriter(anonymous);

        await writer.LogInsertAsync(connection, "Widget", SampleWidget(), CancellationToken.None);

        Assert.Equal(RowAuditWriter.SystemUserName, Assert.Single(connection.Commands)["UserName"]);
    }

    [Fact]
    public async Task UserName_IsTheUserNameClaimOfTheCurrentRequest()
    {
        var connection = new RecordingDbConnection();
        var writer = CreateWriter(Signed("Ken Tsai"));

        await writer.LogDeleteAsync(connection, "Widget", SampleWidget(), CancellationToken.None);

        Assert.Equal("Ken Tsai", Assert.Single(connection.Commands)["UserName"]);
    }

    [Fact]
    public async Task UserName_FallsBackToIdentityName_WhenTheUserNameClaimIsMissing()
    {
        var connection = new RecordingDbConnection();
        var writer = CreateWriter(AuthenticatedUser(userName: null, userId: "u001"));

        await writer.LogInsertAsync(connection, "Widget", SampleWidget(), CancellationToken.None);

        Assert.Equal("u001", Assert.Single(connection.Commands)["UserName"]);
    }

    // ---- Truncation, timestamp, transaction ----

    [Fact]
    public async Task ActionDesc_IsTruncatedTo1000Characters()
    {
        var connection = new RecordingDbConnection();
        var writer = CreateWriter(Signed());
        var widget = SampleWidget();
        widget.Code = new string('x', 1500);

        await writer.LogInsertAsync(connection, "Widget", widget, CancellationToken.None);

        var actionDesc = Assert.IsType<string>(Assert.Single(connection.Commands)["ActionDesc"]);
        Assert.Equal(RowAuditWriter.ActionDescMaxLength, actionDesc.Length);
        Assert.Equal(widget.Code[..RowAuditWriter.ActionDescMaxLength], actionDesc);
    }

    [Fact]
    public async Task DateTime_IsTheTimeProvidersUtcNow()
    {
        var connection = new RecordingDbConnection();
        var writer = CreateWriter(Signed(), new DateTimeOffset(2026, 9, 8, 10, 30, 0, TimeSpan.FromHours(8)));

        await writer.LogInsertAsync(connection, "Widget", SampleWidget(), CancellationToken.None);

        var dateTime = Assert.IsType<DateTime>(Assert.Single(connection.Commands)["DateTime"]);
        Assert.Equal(new DateTime(2026, 9, 8, 2, 30, 0, DateTimeKind.Utc), dateTime);
        Assert.Equal(DateTimeKind.Utc, dateTime.Kind);
    }

    [Fact]
    public async Task Command_RunsOnTheCallersTransaction()
    {
        var connection = new RecordingDbConnection();
        await using var transaction = await connection.BeginTransactionAsync();
        var writer = CreateWriter(Signed());

        await writer.LogInsertAsync(connection, "Widget", SampleWidget(), CancellationToken.None, transaction);

        Assert.Same(transaction, Assert.Single(connection.Commands).Transaction);
    }

    [Fact]
    public async Task TableName_IsWrittenAsPassed()
    {
        var connection = new RecordingDbConnection();
        var writer = CreateWriter(Signed());

        await writer.LogInsertAsync(connection, "Course", SampleWidget(), CancellationToken.None);

        Assert.Equal("Course", Assert.Single(connection.Commands)["TableName"]);
    }
}
