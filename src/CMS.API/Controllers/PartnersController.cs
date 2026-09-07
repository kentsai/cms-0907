using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

[ApiController]
[Route("api/partners")]
[Produces("application/json")]
public class PartnersController(IPartnerRepository repository) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<Partner>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Partner>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await repository.GetAllAsync(cancellationToken));
    }

    [HttpPost("query")]
    [ProducesResponseType<IReadOnlyList<Partner>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Partner>>> Query(
        [FromBody] PartnerQuery query, CancellationToken cancellationToken)
    {
        return Ok(await repository.QueryAsync(query, cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<Partner>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Partner>> GetById(short id, CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [ProducesResponseType<Partner>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Partner>> Create(
        [FromBody] PartnerRequest request, CancellationToken cancellationToken)
    {
        var pkid = await repository.CreateAsync(request, cancellationToken);
        var created = new Partner
        {
            Pkid = pkid,
            Name = request.Name,
            AppKey = request.AppKey,
            NameOnPartnerMenu = request.NameOnPartnerMenu,
            NameOnCourseDetailPage = request.NameOnCourseDetailPage,
            DisplayOrder = request.DisplayOrder,
            ImageFilename = string.IsNullOrWhiteSpace(request.ImageFilename) ? null : request.ImageFilename
        };

        return CreatedAtAction(nameof(GetById), new { id = pkid }, created);
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] PartnerRequest request, CancellationToken cancellationToken)
    {
        var updated = await repository.UpdateAsync(request, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(short id, CancellationToken cancellationToken)
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
