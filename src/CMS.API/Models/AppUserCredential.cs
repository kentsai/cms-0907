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

    /// <summary>
    /// The stored password hash, in whichever format the row holds: a salted PBKDF2 string written by
    /// <see cref="Infrastructure.PasswordHasher.Hash"/>, or a legacy unsalted SHA-256 hex digest on a row not yet
    /// rewritten. Only <see cref="Infrastructure.PasswordHasher.Verify"/> reads it; never compare it directly.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;
}
