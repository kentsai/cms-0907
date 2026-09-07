using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>String-keyed entity: <c>{id}</c> is <c>RoleId</c>, so routes carry no <c>:int</c> constraint.</summary>
[ApiController]
[Route("api/app-roles")]
[Produces("application/json")]
public class AppRolesController(IAppRoleRepository repository) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AppRole>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppRole>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await repository.GetAllAsync(cancellationToken));
    }

    [HttpPost("query")]
    [ProducesResponseType<IReadOnlyList<AppRole>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppRole>>> Query([FromBody] AppRoleQuery query, CancellationToken cancellationToken)
    {
        return Ok(await repository.QueryAsync(query, cancellationToken));
    }

    [HttpGet("{id}")]
    [ProducesResponseType<AppRole>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppRole>> GetById(string id, CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [ProducesResponseType<AppRole>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppRole>> Create([FromBody] AppRoleRequest request, CancellationToken cancellationToken)
    {
        request.RoleId = request.RoleId.Trim();
        if (await repository.ExistsAsync(request.RoleId, cancellationToken))
        {
            return Conflict(new { message = $"角色代碼「{request.RoleId}」已存在。" });
        }

        var pkid = await repository.CreateAsync(request, cancellationToken);
        var created = new AppRole
        {
            Pkid = pkid,
            RoleId = request.RoleId,
            RoleName = request.RoleName,
            PermissionLevel = request.PermissionLevel,
            Description = request.Description,
            UserCount = request.UserIds.Count,
            UserIds = request.UserIds
        };

        return CreatedAtAction(nameof(GetById), new { id = request.RoleId }, created);
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] AppRoleRequest request, CancellationToken cancellationToken)
    {
        request.RoleId = request.RoleId.Trim();
        var updated = await repository.UpdateAsync(request, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
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
