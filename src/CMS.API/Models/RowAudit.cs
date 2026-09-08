namespace CMS.API.Models;

/// <summary>
/// One entry of a record's audit trail as returned by <c>GET /api/row-audits?tableName=&amp;pkid=</c>: the
/// four columns of <c>dbo.RowAudit</c> the history badge shows. <see cref="DateTime"/> is stored as UTC but
/// Dapper returns it with <c>Kind = Unspecified</c>; the frontend appends <c>'Z'</c> before parsing.
/// </summary>
public class RowAudit
{
    public DateTime DateTime { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string? ActionDesc { get; set; }
}
