using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// Write DTO for create/update. <see cref="Pkid"/> is ignored on create (IDENTITY) and identifies the row on
/// update. Carries the two N-N id lists; the repository replaces the junction rows on every save.
/// </summary>
public class CourseRequest : IValidatableObject
{
    /// <summary>Printable ASCII without whitespace — <c>CourseId</c> and <c>ProdCourseId</c> are <c>varchar</c>.</summary>
    public const string AsciiPattern = @"^[\x21-\x7E]+$";

    public int Pkid { get; set; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(300)]
    public string? OfficialTitle { get; set; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]
    [RegularExpression(AsciiPattern, ErrorMessage = "簡介代碼只能包含英數字與符號（不可含空白或中文）。")]
    public string CourseId { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]
    [RegularExpression(AsciiPattern, ErrorMessage = "科目代碼只能包含英數字與符號（不可含空白或中文）。")]
    public string ProdCourseId { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    public string FriendlyUrl { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    [Range(1, short.MaxValue, ErrorMessage = "請選擇原廠。")]
    public short PartnerPkid { get; set; }

    public short? CourseGroupPkid { get; set; }

    [Range(1, byte.MaxValue, ErrorMessage = "請選擇上架狀態。")]
    public byte PublishStatusPkid { get; set; }

    public DateOnly ScheduleOn { get; set; }

    public DateOnly ScheduleOff { get; set; }

    [Range(0, short.MaxValue)]
    public short Hour { get; set; }

    /// <summary>decimal(9,0)</summary>
    [Range(0, 999_999_999)]
    public decimal ListPrice { get; set; }

    /// <summary>decimal(9,1)</summary>
    [Range(0, 99_999_999.9)]
    public decimal LearningCredit { get; set; }

    [StringLength(500)]
    public string? Material { get; set; }

    [StringLength(4000)]
    public string? Objective { get; set; }

    [StringLength(500)]
    public string? Target { get; set; }

    [StringLength(4000)]
    public string? Prerequisites { get; set; }

    public string? Outline { get; set; }

    public string? TowardCertOrExam { get; set; }

    [StringLength(4000)]
    public string? Note { get; set; }

    [StringLength(4000)]
    public string? OtherInfo { get; set; }

    public bool CanRepeat { get; set; }

    public List<int> CertificationPkids { get; set; } = [];

    public List<short> JobCategoryPkids { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ScheduleOff < ScheduleOn)
        {
            yield return new ValidationResult("下架日期不可早於上架日期。", [nameof(ScheduleOff)]);
        }
    }
}
