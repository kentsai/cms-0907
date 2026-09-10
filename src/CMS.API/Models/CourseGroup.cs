namespace CMS.API.Models;

/// <summary>
/// Response model for <c>dbo.CourseGroup</c> — classification buckets that courses are filed under.
/// </summary>
public class CourseGroup
{
    public short Pkid { get; set; }
    public string Description { get; set; } = string.Empty;
}
