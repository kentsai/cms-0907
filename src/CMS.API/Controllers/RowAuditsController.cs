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

    /// <summary>
    /// <c>GET /api/row-audits?tableName=Course&amp;pkid=123</c> — every audit row of that record, newest first,
    /// each with <c>DateTime</c>, <c>UserName</c>, <c>ActionType</c> and <c>ActionDesc</c>. <c>pkid</c> is the
    /// value the writer stored as <c>PrimaryKeyValues</c> (the numeric pkid, or the string key of AppRole / AppUser).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RowAudit>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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

        return Ok(await repository.GetForRecordAsync(tableName!.Trim(), pkid!.Trim(), cancellationToken));
    }
}
