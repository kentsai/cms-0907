using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>Read side of <c>dbo.RowAudit</c>: the audit trail of one record for the 異動紀錄 History badge.</summary>
[ApiController]
[Route("api/row-audits")]
[Produces("application/json")]
public class RowAuditsController(IRowAuditRepository repository) : ControllerBase
{
    public const string TableNameRequiredMessage = "tableName is required.";
    public const string PkidRequiredMessage = "pkid is required.";
    public const string AdminTableForbiddenMessage = "只有管理者可以查看帳號與角色的異動紀錄。";

    /// <summary>
    /// Audit trails that only an administrator may read. <c>tableName</c> arrives from the query string, so
    /// without this the Admin policy on <c>AppUsersController</c> / <c>AppRolesController</c> guards the rows
    /// but not their history: any signed-in caller could read who changed which account and when. Matched
    /// case-insensitively because the value is caller-supplied.
    /// </summary>
    private static readonly HashSet<string> AdminOnlyTables =
        new(StringComparer.OrdinalIgnoreCase) { "AppUser", "AppRole" };

    /// <summary>
    /// <c>GET /api/row-audits?tableName=Course&amp;pkid=123</c> — every audit row of that record, newest first,
    /// each with <c>DateTime</c>, <c>UserName</c>, <c>ActionType</c> and <c>ActionDesc</c>. <c>pkid</c> is the
    /// value the writer stored as <c>PrimaryKeyValues</c> (the numeric pkid, or the string key of AppRole / AppUser).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RowAudit>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<RowAudit>>> GetForRecord(
        [FromQuery] string? tableName, [FromQuery] string? pkid, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            ModelState.AddModelError(nameof(tableName), TableNameRequiredMessage);
        }
        if (string.IsNullOrWhiteSpace(pkid))
        {
            ModelState.AddModelError(nameof(pkid), PkidRequiredMessage);
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(modelStateDictionary: ModelState, statusCode: StatusCodes.Status400BadRequest);
        }

        var table = tableName!.Trim();
        // `!= true` rather than `!`: a null principal fails closed. The global AuthorizeFilter means this
        // action never runs unauthenticated in production, but the check should not depend on that.
        if (AdminOnlyTables.Contains(table) && User?.IsInRole(AuthorizationPolicies.AdminRole) != true)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = AdminTableForbiddenMessage });
        }

        return Ok(await repository.GetForRecordAsync(table, pkid!.Trim(), cancellationToken));
    }
}
