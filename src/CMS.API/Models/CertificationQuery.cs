namespace CMS.API.Models;

/// <summary>
/// Search DTO for <c>POST /api/certifications/query</c>. Every member is optional; null means "no filter".
/// </summary>
public class CertificationQuery
{
    /// <summary>LIKE match on <c>RTRIM(Title)</c>.</summary>
    public string? Keyword { get; set; }

    public short? PartnerPkid { get; set; }

    /// <summary>Only certifications linked to this course via <c>CourseInCertification</c>.</summary>
    public int? CoursePkid { get; set; }

    /// <summary>Only certifications linked to this job category via <c>CertificationJobCategories</c>.</summary>
    public short? JobCategoryPkid { get; set; }
}
