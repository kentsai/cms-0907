namespace CMS.API.Models;

/// <summary>
/// Response model for <c>dbo.AppRole</c>. The clustered primary key is the string <see cref="RoleId"/>;
/// <see cref="Pkid"/> is an IDENTITY surrogate shown as 主代碼 only.
/// </summary>
public class AppRole
{
    public int Pkid { get; set; }
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public int PermissionLevel { get; set; }
    public string? Description { get; set; }

    /// <summary>Number of <c>AppUserRole</c> rows for this role (subquery in SELECT).</summary>
    public int UserCount { get; set; }

    /// <summary>Assigned user ids. Populated on GET by id only.</summary>
    public List<string> UserIds { get; set; } = [];
}
