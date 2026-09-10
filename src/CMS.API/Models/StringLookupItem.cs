namespace CMS.API.Models;

/// <summary>
/// Lookup pair for tables whose key is a string (e.g. <c>AppRole.RoleId</c>, <c>AppUser.UserId</c>).
/// <see cref="LookupItem"/> covers numeric keys.
/// </summary>
public class StringLookupItem
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}
