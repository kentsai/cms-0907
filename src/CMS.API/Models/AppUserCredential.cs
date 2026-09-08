namespace CMS.API.Models;

/// <summary>
/// Backend-only projection of <c>dbo.AppUser</c> used to verify a login. This is the one place
/// <c>PasswordHash</c> is read; it must never be returned from a controller — <see cref="LoginResponse"/>
/// is the wire model.
/// </summary>
public sealed class AppUserCredential
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    /// <summary>SHA-256 hex of the password (see <see cref="Infrastructure.PasswordHasher"/>).</summary>
    public string PasswordHash { get; set; } = string.Empty;
}
