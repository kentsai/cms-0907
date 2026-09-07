using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// Write DTO for create/update. <see cref="RoleId"/> is the caller-assigned string key; it is immutable on update.
/// </summary>
public class AppRoleRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    public string RoleId { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    public string RoleName { get; set; } = string.Empty;

    /// <summary>Mirrors DB default <c>DF_AppRole_Privilege</c> (100).</summary>
    public int PermissionLevel { get; set; } = 100;

    [StringLength(400)]
    public string? Description { get; set; }

    /// <summary>Users assigned to this role (synced into <c>AppUserRole</c>).</summary>
    public List<string> UserIds { get; set; } = [];
}
