using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// String-keyed entity: <c>{id}</c> is <c>UserId</c>, so routes carry no <c>:int</c> constraint.
/// The password hash never crosses this boundary — creation seeds it from the system default and
/// <see cref="ResetPassword"/> is the only way to change it.
/// <para>
/// Administrators only (<see cref="AuthorizationPolicies.Admin"/>, on top of the global authentication filter):
/// these actions create accounts, delete them, change role membership and reset a password to the shared system
/// default. A signed-in non-administrator gets <b>403</b> and the repository is never reached.
/// </para>
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/app-users")]
[Produces("application/json")]
public class AppUsersController(IAppUserRepository repository, IPasswordStampCache passwordStamps) : ControllerBase
{
    private const string AppConfigErrorTitle = "系統設定錯誤";

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AppUser>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppUser>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await repository.GetAllAsync(cancellationToken));
    }

    [HttpPost("query")]
    [ProducesResponseType<IReadOnlyList<AppUser>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppUser>>> Query([FromBody] AppUserQuery query, CancellationToken cancellationToken)
    {
        return Ok(await repository.QueryAsync(query, cancellationToken));
    }

    [HttpGet("{id}")]
    [ProducesResponseType<AppUser>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppUser>> GetById(string id, CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [ProducesResponseType<AppUser>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AppUser>> Create([FromBody] AppUserRequest request, CancellationToken cancellationToken)
    {
        request.UserId = request.UserId.Trim();
        if (await repository.ExistsAsync(request.UserId, cancellationToken))
        {
            return Conflict(new { message = $"使用者代碼「{request.UserId}」已存在。" });
        }

        int pkid;
        try
        {
            pkid = await repository.CreateAsync(request, cancellationToken);
        }
        catch (DuplicateKeyException ex)
        {
            // The ExistsAsync check above is a read followed by a write, so a concurrent create with the
            // same key slips past it and loses on the primary key instead. Same 409 either way.
            return Conflict(new { message = ex.Message });
        }
        catch (AppConfigException ex)
        {
            return Problem(detail: ex.Message, title: AppConfigErrorTitle, statusCode: StatusCodes.Status500InternalServerError);
        }

        var created = new AppUser
        {
            Pkid = pkid,
            UserId = request.UserId,
            UserName = request.UserName,
            IsActive = request.IsActive,
            PasswordUpdatedTime = DateTime.UtcNow,
            RoleCount = request.RoleIds.Count,
            RoleIds = request.RoleIds
        };

        return CreatedAtAction(nameof(GetById), new { id = request.UserId }, created);
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] AppUserRequest request, CancellationToken cancellationToken)
    {
        request.UserId = request.UserId.Trim();
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

    /// <summary>Resets the user's password to the system default (<c>SysConfig.appConfig.defaultPassword</c>).</summary>
    [HttpPost("{id}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ResetPassword(string id, CancellationToken cancellationToken)
    {
        try
        {
            var reset = await repository.ResetPasswordAsync(id, cancellationToken);
            if (!reset)
            {
                return NotFound();
            }

            // Same reason as AuthController.ChangePassword: the bearer handler caches PasswordUpdatedTime
            // per user, so without this the target's existing token keeps working for up to
            // PasswordStampCache.CacheDuration. This reset is the one lever an admin has to end a
            // hijacked session, so it has to take effect on the very next request.
            passwordStamps.Invalidate(id);
            return NoContent();
        }
        catch (AppConfigException ex)
        {
            return Problem(detail: ex.Message, title: AppConfigErrorTitle, statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
