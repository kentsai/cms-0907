using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// Write DTO for create/update. <see cref="Pkid"/> is ignored on create (the column is IDENTITY)
/// and identifies the row on update (PUT takes it from the body).
/// </summary>
public class PartnerRequest
{
    /// <summary>Printable ASCII without whitespace — <c>AppKey</c> and <c>ImageFilename</c> are <c>varchar</c> columns.</summary>
    public const string AsciiPattern = @"^[\x21-\x7E]+$";

    public short Pkid { get; set; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [StringLength(10)]
    [RegularExpression(AsciiPattern, ErrorMessage = "AppKey 只能包含英數字與符號（不可含空白或中文）。")]
    public string AppKey { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    public string NameOnPartnerMenu { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]
    public string NameOnCourseDetailPage { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    [StringLength(50)]
    [RegularExpression(AsciiPattern, ErrorMessage = "圖片檔名只能包含英數字與符號（不可含空白或中文）。")]
    public string? ImageFilename { get; set; }
}
