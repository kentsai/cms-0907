using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// Write DTO for create/update. <see cref="Pkid"/> is ignored on create (IDENTITY) and identifies the row on
/// update. Carries the two N-N id lists; the repository replaces the junction rows on every save.
/// </summary>
public class CertificationRequest
{
    public int Pkid { get; set; }

    [Range(1, short.MaxValue, ErrorMessage = "請選擇原廠。")]
    public short PartnerPkid { get; set; }

    /// <summary><c>nchar(100)</c>, optional. Trimmed on save; blank becomes NULL.</summary>
    [StringLength(100)]
    public string? Title { get; set; }

    public List<int> CoursePkids { get; set; } = [];

    public List<short> JobCategoryPkids { get; set; } = [];
}
