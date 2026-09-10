using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>Body of <c>POST /api/auth/login</c>.</summary>
public class LoginRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Plain-text password; verified against <c>AppUser.PasswordHash</c> by
    /// <see cref="Infrastructure.PasswordHasher.Verify"/> (salted PBKDF2, with legacy SHA-256 rows rewritten in
    /// place on success). Never stored or logged.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Password { get; set; } = string.Empty;
}
