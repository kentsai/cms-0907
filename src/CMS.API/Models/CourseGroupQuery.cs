namespace CMS.API.Models;

/// <summary>
/// Search DTO for <c>POST /api/course-groups/query</c>. Every member is optional; null means "no filter".
/// </summary>
public class CourseGroupQuery
{
    /// <summary>LIKE match on <c>Description</c>.</summary>
    public string? Keyword { get; set; }
}
