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

    [Fact]
    public void ChangedColumns_SkipsAuditIgnoredProperties()
    {
        var before = new Course { Pkid = 1, PartnerPkid = 1, PartnerName = "Microsoft", PublishStatusDescription = "Draft" };
        var after = new Course { Pkid = 1, PartnerPkid = 2, PartnerName = "Amazon", PublishStatusDescription = "Published" };

        Assert.Equal(new[] { nameof(Course.PartnerPkid) }, AuditHelper.ChangedColumns(before, after));
    }

    [Fact]
    public void ChangedColumns_ComparesSequencesByElements_NotByReference()
    {
        var before = new Course { Pkid = 1, CourseId = "C1", CertificationPkids = [1, 2], JobCategoryPkids = [3] };
        var same = new Course { Pkid = 1, CourseId = "C1", CertificationPkids = [1, 2], JobCategoryPkids = [3] };
        var reordered = new Course { Pkid = 1, CourseId = "C1", CertificationPkids = [2, 1], JobCategoryPkids = [3] };

        Assert.Empty(AuditHelper.ChangedColumns(before, same));
        Assert.Equal(new[] { nameof(Course.CertificationPkids) }, AuditHelper.ChangedColumns(before, reordered));
    }
}
