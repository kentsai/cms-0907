using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// Administrators only (<see cref="AuthorizationPolicies.Admin"/>, on top of the global authentication filter):
/// these rows are the publish-state vocabulary every course's 上架狀態 points at, not per-course content, and the
/// 系統管理 Admin menu group has always presented them that way. A signed-in non-administrator gets <b>403</b>.
/// <para>
/// The read-only lookup used to populate the course form's status dropdown lives on
/// <c>GET /api/lookups/publish-statuses</c> and stays open to every signed-in user — locking it here would
/// empty that dropdown for the editors who need it.
/// </para>
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/publish-statuses")]
[Produces("application/json")]
public class PublishStatusesController(IPublishStatusRepository repository) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PublishStatus>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PublishStatus>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await repository.GetAllAsync(cancellationToken));
    }

    [HttpPost("query")]
    [ProducesResponseType<IReadOnlyList<PublishStatus>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PublishStatus>>> Query(
        [FromBody] PublishStatusQuery query, CancellationToken cancellationToken)
    {
        return Ok(await repository.QueryAsync(query, cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<PublishStatus>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublishStatus>> GetById(byte id, CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [ProducesResponseType<PublishStatus>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PublishStatus>> Create(
        [FromBody] PublishStatusRequest request, CancellationToken cancellationToken)
    {
        if (await repository.ExistsAsync(request.Pkid, cancellationToken))
        {
            return Conflict(new { message = $"主代碼 {request.Pkid} 已存在。" });
        }

        var pkid = await repository.CreateAsync(request, cancellationToken);
        var created = new PublishStatus
        {
            Pkid = pkid,
            Description = request.Description,
            IsDraft = request.IsDraft,
            IsPublished = request.IsPublished,
            IsDiscontinued = request.IsDiscontinued
        };

        return CreatedAtAction(nameof(GetById), new { id = pkid }, created);
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] PublishStatusRequest request, CancellationToken cancellationToken)
    {
        var updated = await repository.UpdateAsync(request, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(byte id, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await repository.DeleteAsync(id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
        catch (EntityInUseException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
