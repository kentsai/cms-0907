namespace CMS.API.Models;

/// <summary>
/// Search DTO for <c>POST /api/publish-statuses/query</c>. Every member is optional; null means "no filter".
/// </summary>
public class PublishStatusQuery
{
    /// <summary>LIKE match on <c>Description</c>.</summary>
    public string? Keyword { get; set; }

    public bool? IsDraft { get; set; }
    public bool? IsPublished { get; set; }
    public bool? IsDiscontinued { get; set; }
}
