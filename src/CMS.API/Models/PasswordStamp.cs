namespace CMS.API.Models;

/// <summary>
/// Backend-only: when an <c>AppUser</c>'s password last changed. Read by the bearer handler (via
/// <see cref="Infrastructure.IPasswordStampCache"/>) to reject tokens issued before that moment, so a password
/// change signs every existing session out. Never returned from a controller.
/// </summary>
public sealed class PasswordStamp
{
    /// <summary><c>AppUser.PasswordUpdatedTime</c> (UTC). Null when the column is null: no restriction applies.</summary>
    public DateTime? PasswordUpdatedTime { get; set; }
}
