using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

[ApiController]
[Route("api/courses")]
[Produces("application/json")]
public class CoursesController(ICourseRepository repository) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<Course>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Course>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await repository.GetAllAsync(cancellationToken));
    }

    [HttpPost("query")]
    [ProducesResponseType<IReadOnlyList<Course>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Course>>> Query(
        [FromBody] CourseQuery query, CancellationToken cancellationToken)
    {
        return Ok(await repository.QueryAsync(query, cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<Course>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Course>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [ProducesResponseType<Course>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Course>> Create(
        [FromBody] CourseRequest request, CancellationToken cancellationToken)
    {
        var pkid = await repository.CreateAsync(request, cancellationToken);

        // Re-read so the response carries the JOINed labels and normalised values.
        var created = await repository.GetByIdAsync(pkid, cancellationToken) ?? new Course
        {
            Pkid = pkid,
            Title = request.Title,
            CourseId = request.CourseId,
            ProdCourseId = request.ProdCourseId,
            FriendlyUrl = request.FriendlyUrl,
            DisplayOrder = request.DisplayOrder,
            PartnerPkid = request.PartnerPkid,
            CourseGroupPkid = request.CourseGroupPkid,
            PublishStatusPkid = request.PublishStatusPkid,
            ScheduleOn = request.ScheduleOn,
            ScheduleOff = request.ScheduleOff,
            Hour = request.Hour,
            ListPrice = request.ListPrice,
            LearningCredit = request.LearningCredit,
            CanRepeat = request.CanRepeat,
            CertificationPkids = request.CertificationPkids,
            JobCategoryPkids = request.JobCategoryPkids
        };

        return CreatedAtAction(nameof(GetById), new { id = pkid }, created);
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] CourseRequest request, CancellationToken cancellationToken)
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
