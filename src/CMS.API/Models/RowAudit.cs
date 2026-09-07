namespace CMS.API.Models;

/// <summary>
/// Row from <c>dbo.RowAudit</c>. <see cref="DateTime"/> is stored as UTC (GETUTCDATE()) but Dapper returns it with
/// <c>Kind = Unspecified</c>; the frontend appends <c>'Z'</c> before parsing.
/// </summary>
public class RowAudit
{
    public int Pkid { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string PrimaryKeyValues { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string? ActionDesc { get; set; }
    public DateTime DateTime { get; set; }
}
