using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// Body of <c>PUT /api/auth/profile</c>. Deliberately carries only <see cref="UserName"/>: the user being
/// updated is the JWT subject, and roles are not editable here, so a <c>userId</c> / <c>roleIds</c> in the
/// JSON is simply ignored by model binding.
/// </summary>
public class UpdateProfileRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    public string UserName { get; set; } = string.Empty;
}
