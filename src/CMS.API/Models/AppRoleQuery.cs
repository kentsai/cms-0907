namespace CMS.API.Models;

/// <summary>Search DTO for <c>POST /api/app-roles/query</c>. Null members mean "no filter".</summary>
public class AppRoleQuery
{
    /// <summary>LIKE match on <c>RoleId</c> and <c>RoleName</c>.</summary>
    public string? Keyword { get; set; }

    public int? PermissionLevel { get; set; }

    /// <summary>Only roles assigned to this user (via <c>AppUserRole</c>).</summary>
    public string? UserId { get; set; }
}
