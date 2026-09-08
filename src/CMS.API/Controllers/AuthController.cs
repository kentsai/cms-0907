using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// Login: verifies <c>{ userId, password }</c> against <c>AppUser</c> and returns a profile with a signed JWT.
/// Every failure (unknown user, inactive user, wrong password) yields the same 401 body so callers cannot
/// tell which check failed. <c>PasswordHash</c> never leaves this controller.
/// <see cref="Login"/> is the only anonymous action in the API (authorization is applied globally in
/// <c>Program.cs</c>); <see cref="UpdateProfile"/> and <see cref="ChangePassword"/> require a token like everything else.
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController(
    IAuthRepository repository,
    IJwtTokenIssuer tokenIssuer,
    TimeProvider timeProvider,
    IPasswordStampCache passwordStamps) : ControllerBase
{
    public const string InvalidCredentialsMessage = "帳號或密碼錯誤。";
    public const string UserNameRequiredMessage = "請輸入姓名。";
    public const string CurrentPasswordRequiredMessage = "請輸入目前密碼。";
    public const string CurrentPasswordIncorrectMessage = "目前密碼錯誤。";
    public const string NewPasswordRequiredMessage = "請輸入新密碼。";
    public const string ConfirmPasswordMismatchMessage = "新密碼與確認新密碼不一致。";
    private const string AppConfigErrorTitle = "系統設定錯誤";

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await repository.GetCredentialAsync(request.UserId, cancellationToken);
        if (user is null || !CredentialsMatch(user, request))
        {
            return Unauthorized(new { message = InvalidCredentialsMessage });
        }

        try
        {
            var roleIds = await repository.GetRoleIdsAsync(user.UserId, cancellationToken);
            var signingKey = await repository.GetSymmetricSecurityKeyAsync(cancellationToken);

            return Ok(new LoginResponse
            {
                UserId = user.UserId,
                UserName = user.UserName,
                AccessToken = tokenIssuer.Issue(user, roleIds, signingKey)
            });
        }
        catch (AppConfigException ex)
        {
            return Problem(detail: ex.Message, title: AppConfigErrorTitle, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Self-service profile edit: sets the caller's own <c>UserName</c>. The user is identified solely by the
    /// <c>userId</c> claim of the bearer token — the body has no <c>userId</c>, and roles cannot be changed here.
    /// </summary>
    [HttpPut("profile")]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProfileResponse>> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        // Validated here as well as by [Required] so the rule holds for callers that bypass model binding.
        var userName = (request.UserName ?? string.Empty).Trim();
        if (userName.Length == 0)
        {
            ModelState.AddModelError(nameof(request.UserName), UserNameRequiredMessage);
            return ValidationProblem(modelStateDictionary: ModelState, statusCode: StatusCodes.Status400BadRequest);
        }

        var updated = await repository.UpdateUserNameAsync(userId, userName, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        return Ok(new ProfileResponse
        {
            UserId = userId,
            UserName = userName,
            Roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList()
        });
    }

    /// <summary>
    /// Self-service password change for the token's user. In order: the current password must hash to the stored
    /// <c>PasswordHash</c> (otherwise nothing changes), the new password must satisfy <see cref="PasswordPolicy"/>,
    /// and the confirmation must match. Every rejection is a 400 <c>ValidationProblem</c> keyed by the offending
    /// field. On success <c>PasswordHash</c> = SHA-256(new) and <c>PasswordUpdatedTime</c> = now; no hash is ever
    /// returned. The new <c>PasswordUpdatedTime</c> also invalidates every token issued before it — including the
    /// one this request was made with — so the caller must log in again with the new password.
    /// </summary>
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        // Re-checked here (not only via [Required]) so the rules hold for callers that bypass model binding.
        if (string.IsNullOrEmpty(request.CurrentPassword))
        {
            return FieldError(nameof(request.CurrentPassword), CurrentPasswordRequiredMessage);
        }

        var user = await repository.GetCredentialAsync(userId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        if (!PasswordMatches(user, request.CurrentPassword))
        {
            return FieldError(nameof(request.CurrentPassword), CurrentPasswordIncorrectMessage);
        }

        if (string.IsNullOrEmpty(request.NewPassword))
        {
            return FieldError(nameof(request.NewPassword), NewPasswordRequiredMessage);
        }

        if (!PasswordPolicy.IsCompliant(request.NewPassword))
        {
            return FieldError(nameof(request.NewPassword), PasswordPolicy.Message);
        }

        if (!string.Equals(request.NewPassword, request.ConfirmNewPassword, StringComparison.Ordinal))
        {
            return FieldError(nameof(request.ConfirmNewPassword), ConfirmPasswordMismatchMessage);
        }

        var updated = await repository.UpdatePasswordAsync(
            userId, PasswordHasher.Sha256Hex(request.NewPassword), timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        // The bearer handler caches PasswordUpdatedTime per user; drop it so the old token dies on the next request.
        passwordStamps.Invalidate(userId);
        return NoContent();
    }

    private ActionResult FieldError(string field, string message)
    {
        ModelState.AddModelError(field, message);
        return ValidationProblem(modelStateDictionary: ModelState, statusCode: StatusCodes.Status400BadRequest);
    }

    /// <summary>The <c>userId</c> claim (the bearer scheme also maps it to <c>Identity.Name</c>).</summary>
    private string? CurrentUserId() =>
        User.FindFirst(JwtTokenIssuer.UserIdClaim)?.Value ?? User.Identity?.Name;

    /// <summary>
    /// UserId must match exactly (ordinal — the DB lookup may be collation-insensitive), the user must be active,
    /// and the stored hash must equal SHA-256(password).
    /// </summary>
    private static bool CredentialsMatch(AppUserCredential user, LoginRequest request)
    {
        var userIdMatches = string.Equals(user.UserId, request.UserId, StringComparison.Ordinal);
        return userIdMatches & user.IsActive & PasswordMatches(user, request.Password);
    }

    /// <summary>SHA-256(password) against the stored hash: constant-time and hex-case-insensitive.</summary>
    private static bool PasswordMatches(AppUserCredential user, string password)
    {
        var expected = Encoding.UTF8.GetBytes(PasswordHasher.Sha256Hex(password));
        var actual = Encoding.UTF8.GetBytes((user.PasswordHash ?? string.Empty).Trim().ToLowerInvariant());
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
