using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// Write DTO for create/update. <see cref="Pkid"/> is included because the column is not IDENTITY:
/// the caller assigns it on create, and it identifies the row on update.
/// </summary>
public class PublishStatusRequest
{
    [Range(0, 255)]
    public byte Pkid { get; set; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]
    public string Description { get; set; } = string.Empty;

    public bool IsDraft { get; set; }
    public bool IsPublished { get; set; }
    public bool IsDiscontinued { get; set; }
}
