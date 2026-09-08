using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>Body of <c>POST /api/auth/login</c>.</summary>
public class LoginRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>Plain-text password; compared as SHA-256 hex against <c>AppUser.PasswordHash</c>, never stored or logged.</summary>
    [Required(AllowEmptyStrings = false)]
    public string Password { get; set; } = string.Empty;
}
