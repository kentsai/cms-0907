namespace CMS.API.Models;

/// <summary>
/// Response model for <c>dbo.AppUser</c>. The clustered primary key is the string <see cref="UserId"/>;
/// <see cref="Pkid"/> is an IDENTITY surrogate shown as 主代碼 only.
/// <c>PasswordHash</c> is deliberately absent — it never leaves the backend.
/// </summary>
public class AppUser
{
    public int Pkid { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    /// <summary>Set by the server on create and on password reset. Read-only for clients.</summary>
    public DateTime? PasswordUpdatedTime { get; set; }

    /// <summary>Number of <c>AppUserRole</c> rows for this user (subquery in SELECT).</summary>
    public int RoleCount { get; set; }

    /// <summary>Assigned role ids. Populated on GET by id only.</summary>
    public List<string> RoleIds { get; set; } = [];
}
