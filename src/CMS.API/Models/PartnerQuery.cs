namespace CMS.API.Models;

/// <summary>
/// Search DTO for <c>POST /api/partners/query</c>. Every member is optional; null means "no filter".
/// </summary>
public class PartnerQuery
{
    /// <summary>LIKE match on <c>Name</c>, <c>AppKey</c>, <c>NameOnPartnerMenu</c> and <c>NameOnCourseDetailPage</c>.</summary>
    public string? Keyword { get; set; }
}
