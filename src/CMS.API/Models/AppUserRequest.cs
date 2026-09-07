using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// Write DTO for create/update. <see cref="UserId"/> is the caller-assigned string key; it is immutable on update.
/// There is intentionally no password member: the hash is seeded server-side on create and changed only by the
/// reset-password endpoint.
/// </summary>
public class AppUserRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    public string UserId { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>Mirrors DB default <c>DF_AppUser_IsActive</c> (1).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Roles assigned to this user (synced into <c>AppUserRole</c>).</summary>
    public List<string> RoleIds { get; set; } = [];
}
