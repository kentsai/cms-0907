using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// Body of <c>POST /api/auth/change-password</c>. Plain-text passwords in, nothing out: the user whose password
/// changes is the JWT subject (there is deliberately no <c>UserId</c> member), the current password is verified
/// against the stored hash, and the new one is hashed before it reaches the repository. Never logged.
/// </summary>
public class ChangePasswordRequest
{
    [Required(AllowEmptyStrings = false)]
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>Must satisfy <see cref="Infrastructure.PasswordPolicy"/>.</summary>
    [Required(AllowEmptyStrings = false)]
    public string NewPassword { get; set; } = string.Empty;

    /// <summary>Must equal <see cref="NewPassword"/> exactly.</summary>
    [Required(AllowEmptyStrings = false)]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
