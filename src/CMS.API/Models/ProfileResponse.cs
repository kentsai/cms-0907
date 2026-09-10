namespace CMS.API.Models;

/// <summary>The signed-in user's profile as returned by <c>PUT /api/auth/profile</c>. Roles come from the token and are read-only.</summary>
public class ProfileResponse
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = [];
}
