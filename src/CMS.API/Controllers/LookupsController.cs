using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// Slim <c>{ pkid, label }</c> lists for FK dropdowns. Add one action per table that is used as an FK target.
/// </summary>
[ApiController]
[Route("api/lookups")]
[Produces("application/json")]
public class LookupsController(IPublishStatusRepository publishStatuses) : ControllerBase
{
    [HttpGet("publish-statuses")]
    [ProducesResponseType<IReadOnlyList<LookupItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LookupItem>>> PublishStatuses(CancellationToken cancellationToken)
    {
        return Ok(await publishStatuses.GetLookupAsync(cancellationToken));
    }
}
