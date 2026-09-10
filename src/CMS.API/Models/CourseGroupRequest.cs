using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// Write DTO for create/update. <see cref="Pkid"/> is ignored on create (the column is IDENTITY)
/// and identifies the row on update (PUT takes it from the body).
/// </summary>
public class CourseGroupRequest
{
    public short Pkid { get; set; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    public string Description { get; set; } = string.Empty;
}
