namespace CMS.API.Models;

/// <summary>
/// Slim <c>{ pkid, label }</c> pair returned by <c>/api/lookups/*</c> endpoints for FK dropdowns.
/// </summary>
public class LookupItem
{
    public int Pkid { get; set; }
    public string Label { get; set; } = string.Empty;
}
