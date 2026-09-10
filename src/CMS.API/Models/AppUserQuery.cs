namespace CMS.API.Models;

/// <summary>Search DTO for <c>POST /api/app-users/query</c>. Null members mean "no filter".</summary>
public class AppUserQuery
{
    /// <summary>LIKE match on <c>UserId</c> and <c>UserName</c>.</summary>
    public string? Keyword { get; set; }

    /// <summary>Tri-state: null = all, true = active only, false = inactive only.</summary>
    public bool? IsActive { get; set; }

    /// <summary>Only users holding this role (via <c>AppUserRole</c>).</summary>
    public string? RoleId { get; set; }
}
