namespace CMS.API.Models;

/// <summary>
/// Response model for <c>dbo.Certification</c>. <see cref="PartnerName"/> is JOINed in every SELECT so list pages
/// show the vendor without a lookup; the two N-N pkid lists are populated only by <c>GetByIdAsync</c>.
/// </summary>
public class Certification
{
    public int Pkid { get; set; }
    public short PartnerPkid { get; set; }

    /// <summary><c>nchar(100)</c>, nullable — always RTRIMmed on read.</summary>
    public string? Title { get; set; }

    /// <summary><c>Partner.Name</c> (INNER JOIN).</summary>
    public string PartnerName { get; set; } = string.Empty;

    /// <summary>Linked <c>Course.pkid</c> values via <c>CourseInCertification</c>.</summary>
    public List<int> CoursePkids { get; set; } = [];

    /// <summary>Linked <c>JobCategory.pkid</c> values via <c>CertificationJobCategories</c>.</summary>
    public List<short> JobCategoryPkids { get; set; } = [];
}
