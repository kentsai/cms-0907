using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

[ApiController]
[Route("api/certifications")]
[Produces("application/json")]
public class CertificationsController(ICertificationRepository repository) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<Certification>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Certification>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await repository.GetAllAsync(cancellationToken));
    }

    [HttpPost("query")]
    [ProducesResponseType<IReadOnlyList<Certification>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Certification>>> Query(
        [FromBody] CertificationQuery query, CancellationToken cancellationToken)
    {
        return Ok(await repository.QueryAsync(query, cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<Certification>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Certification>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [ProducesResponseType<Certification>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Certification>> Create(
        [FromBody] CertificationRequest request, CancellationToken cancellationToken)
    {
        var pkid = await repository.CreateAsync(request, cancellationToken);

        // Re-read so the response carries the JOINed partner name and normalised values.
        var created = await repository.GetByIdAsync(pkid, cancellationToken) ?? new Certification
        {
            Pkid = pkid,
            PartnerPkid = request.PartnerPkid,
            Title = request.Title,
            CoursePkids = request.CoursePkids,
            JobCategoryPkids = request.JobCategoryPkids
        };

        return CreatedAtAction(nameof(GetById), new { id = pkid }, created);
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] CertificationRequest request, CancellationToken cancellationToken)
    {
        var updated = await repository.UpdateAsync(request, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
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
