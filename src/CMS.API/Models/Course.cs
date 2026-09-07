namespace CMS.API.Models;

/// <summary>
/// Response model for <c>dbo.Course</c>. FK label columns are JOINed in every SELECT so list pages can
/// show names without extra lookups; the N-N pkid lists are populated only by <c>GetByIdAsync</c>.
/// </summary>
public class Course
{
    public int Pkid { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? OfficialTitle { get; set; }
    public string CourseId { get; set; } = string.Empty;
    public string ProdCourseId { get; set; } = string.Empty;
    public string FriendlyUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public short PartnerPkid { get; set; }
    public short? CourseGroupPkid { get; set; }
    public byte PublishStatusPkid { get; set; }
    public DateOnly ScheduleOn { get; set; }
    public DateOnly ScheduleOff { get; set; }
    public short Hour { get; set; }
    public decimal ListPrice { get; set; }
    public decimal LearningCredit { get; set; }
    public string? Material { get; set; }
    public string? Objective { get; set; }
    public string? Target { get; set; }
    public string? Prerequisites { get; set; }
    public string? Outline { get; set; }
    public string? TowardCertOrExam { get; set; }
    public string? Note { get; set; }
    public string? OtherInfo { get; set; }
    public bool CanRepeat { get; set; }

    /// <summary><c>Partner.Name</c> (INNER JOIN).</summary>
    public string PartnerName { get; set; } = string.Empty;

    /// <summary><c>CourseGroup.Description</c> (LEFT JOIN — null when the course has no group).</summary>
    public string? CourseGroupDescription { get; set; }

    /// <summary><c>PublishStatus.Description</c> (INNER JOIN).</summary>
    public string PublishStatusDescription { get; set; } = string.Empty;

    /// <summary>Linked <c>Certification.pkid</c> values via <c>CourseInCertification</c>.</summary>
    public List<int> CertificationPkids { get; set; } = [];

    /// <summary>Linked <c>JobCategory.pkid</c> values via <c>CourseJobCategories</c>.</summary>
    public List<short> JobCategoryPkids { get; set; } = [];
}
