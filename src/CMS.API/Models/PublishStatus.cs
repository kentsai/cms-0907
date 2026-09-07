namespace CMS.API.Models;

/// <summary>
/// Response model for <c>dbo.PublishStatus</c> — a lookup table whose key is assigned by the user (tinyint, not IDENTITY).
/// </summary>
public class PublishStatus
{
    public byte Pkid { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsDraft { get; set; }
    public bool IsPublished { get; set; }
    public bool IsDiscontinued { get; set; }
}
