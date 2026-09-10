namespace CMS.API.Infrastructure;

/// <summary>
/// Named authorization policies registered in <c>Program.cs</c>.
/// <para>
/// The API's baseline is the global <c>AuthorizeFilter</c>: every action needs an authenticated user. That is
/// enough for the content tables, but not for the endpoints that hand out access itself — creating accounts,
/// resetting passwords and assigning roles. Those carry <see cref="Admin"/> as well, so the server enforces the
/// boundary the Angular shell only draws visually by hiding the 系統管理 Admin menu group.
/// </para>
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Policy name: the caller's token must carry the <see cref="AdminRole"/> role claim.</summary>
    public const string Admin = "Admin";

    /// <summary>
    /// The <c>AppUserRole.RoleId</c> that grants administration. It matches <c>ADMIN_ROLE</c> in the Angular
    /// <c>auth.service.ts</c>, which decides whether the 系統管理 Admin menu group is rendered.
    /// </summary>
    public const string AdminRole = "Admin";
}
