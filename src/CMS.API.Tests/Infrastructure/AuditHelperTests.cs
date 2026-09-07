using CMS.API.Infrastructure;
using CMS.API.Models;

namespace CMS.API.Tests.Infrastructure;

public class AuditHelperTests
{
    [Fact]
    public void ChangedColumns_ReturnsOnlyPropertiesWhoseValuesDiffer()
    {
        var before = new PublishStatus { Pkid = 1, Description = "草稿", IsDraft = true, IsPublished = false, IsDiscontinued = false };
        var after = new PublishStatusRequest { Pkid = 1, Description = "草稿(修)", IsDraft = true, IsPublished = true, IsDiscontinued = false };

        var changed = AuditHelper.ChangedColumns(before, after);

        Assert.Equal(new[] { "Description", "IsPublished" }, changed);
    }

    [Fact]
    public void ChangedColumns_ReturnsEmpty_WhenNothingChanged()
    {
        var before = new PublishStatus { Pkid = 1, Description = "草稿", IsDraft = true };
        var after = new PublishStatusRequest { Pkid = 1, Description = "草稿", IsDraft = true };

        Assert.Empty(AuditHelper.ChangedColumns(before, after));
    }
}
