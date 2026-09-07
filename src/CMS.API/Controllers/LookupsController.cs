using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// Slim lookup lists for FK dropdowns. Add one action per table that is used as an FK target.
/// Numeric keys return <see cref="LookupItem"/>; string keys return <see cref="StringLookupItem"/>.
/// </summary>
[ApiController]
[Route("api/lookups")]
[Produces("application/json")]
public class LookupsController(
    IPublishStatusRepository publishStatuses,
    IAppRoleRepository appRoles,
    IPartnerRepository partners,
    ICourseGroupRepository courseGroups,
    ICourseRepository courses,
    IAppUserRepository appUsers,
    ILookupRepository lookups) : ControllerBase
{
    [HttpGet("publish-statuses")]
    [ProducesResponseType<IReadOnlyList<LookupItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LookupItem>>> PublishStatuses(CancellationToken cancellationToken)
    {
        return Ok(await publishStatuses.GetLookupAsync(cancellationToken));
    }

    [HttpGet("app-roles")]
    [ProducesResponseType<IReadOnlyList<StringLookupItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StringLookupItem>>> AppRoles(CancellationToken cancellationToken)
    {
        return Ok(await appRoles.GetLookupAsync(cancellationToken));
    }

    [HttpGet("app-users")]
    [ProducesResponseType<IReadOnlyList<StringLookupItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StringLookupItem>>> AppUsers(CancellationToken cancellationToken)
    {
        return Ok(await appUsers.GetLookupAsync(cancellationToken));
    }

    [HttpGet("partners")]
    [ProducesResponseType<IReadOnlyList<LookupItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LookupItem>>> Partners(CancellationToken cancellationToken)
    {
        return Ok(await partners.GetLookupAsync(cancellationToken));
    }

    [HttpGet("course-groups")]
    [ProducesResponseType<IReadOnlyList<LookupItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LookupItem>>> CourseGroups(CancellationToken cancellationToken)
    {
        return Ok(await courseGroups.GetLookupAsync(cancellationToken));
    }

    [HttpGet("courses")]
    [ProducesResponseType<IReadOnlyList<LookupItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LookupItem>>> Courses(CancellationToken cancellationToken)
    {
        return Ok(await courses.GetLookupAsync(cancellationToken));
    }

    [HttpGet("certifications")]
    [ProducesResponseType<IReadOnlyList<LookupItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LookupItem>>> Certifications(CancellationToken cancellationToken)
    {
        return Ok(await lookups.GetCertificationsAsync(cancellationToken));
    }

    [HttpGet("job-categories")]
    [ProducesResponseType<IReadOnlyList<LookupItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LookupItem>>> JobCategories(CancellationToken cancellationToken)
    {
        return Ok(await lookups.GetJobCategoriesAsync(cancellationToken));
    }
}
