namespace CMS.API.Models;

/// <summary>
/// Search DTO for <c>POST /api/courses/query</c>. Every member is optional; null means "no filter".
/// </summary>
public class CourseQuery
{
    /// <summary>LIKE match on <c>Title</c>, <c>OfficialTitle</c>, <c>CourseId</c>, <c>ProdCourseId</c>, <c>FriendlyUrl</c>.</summary>
    public string? Keyword { get; set; }

    public short? PartnerPkid { get; set; }
    public short? CourseGroupPkid { get; set; }
    public byte? PublishStatusPkid { get; set; }

    /// <summary>Inclusive lower bound on <c>ScheduleOn</c>.</summary>
    public DateOnly? ScheduleOnFrom { get; set; }

    /// <summary>Inclusive upper bound on <c>ScheduleOn</c>.</summary>
    public DateOnly? ScheduleOnTo { get; set; }

    /// <summary>Inclusive lower bound on <c>ScheduleOff</c>.</summary>
    public DateOnly? ScheduleOffFrom { get; set; }

    /// <summary>Inclusive upper bound on <c>ScheduleOff</c>.</summary>
    public DateOnly? ScheduleOffTo { get; set; }

    /// <summary>Tri-state: null = all, true = only repeatable, false = only non-repeatable.</summary>
    public bool? CanRepeat { get; set; }
}
