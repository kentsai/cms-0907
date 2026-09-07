using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

[ApiController]
[Route("api/row-audits")]
[Produces("application/json")]
public class RowAuditsController(IRowAuditRepository repository) : ControllerBase
{
    private const int MaxTake = 100;

    /// <summary>Audit trail for one record, newest first. Drives the "last changed" badge on detail/edit pages.</summary>
    [HttpGet("{tableName}/{primaryKeyValues}")]
    [ProducesResponseType<IReadOnlyList<RowAudit>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RowAudit>>> GetForRow(
        string tableName, string primaryKeyValues, [FromQuery] int take = 20, CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, MaxTake);
        return Ok(await repository.GetForRowAsync(tableName, primaryKeyValues, take, cancellationToken));
    }
}
