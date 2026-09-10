using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CMS.API.Infrastructure;

/// <summary>
/// Global MVC authorization filter (registered in <c>Program.cs</c> after the <c>AuthorizeFilter</c>): a token whose
/// <see cref="JwtTokenIssuer.MustChangePasswordClaim"/> is set — issued when the login used the system default
/// password — may only reach actions marked <see cref="AllowPasswordChangeRequiredAttribute"/>. Everything else
/// answers <b>403</b> <c>{ message }</c> (not 401, so the SPA keeps the session and shows the change-password page
/// instead of logging the user out). Anonymous requests carry no claim and pass through.
/// </summary>
public sealed class PasswordChangeRequiredFilter : IAuthorizationFilter
{
    public const string PasswordChangeRequiredMessage = "請先變更密碼後再使用系統。";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.Result is not null)
        {
            // An earlier filter (the global AuthorizeFilter) already rejected the request.
            return;
        }

        if (!MustChangePassword(context.HttpContext.User))
        {
            return;
        }

        if (context.ActionDescriptor.EndpointMetadata.OfType<AllowPasswordChangeRequiredAttribute>().Any())
        {
            return;
        }

        context.Result = new ObjectResult(new { message = PasswordChangeRequiredMessage })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }

    /// <summary>True when the (authenticated) principal carries the must-change-password claim with its expected value.</summary>
    public static bool MustChangePassword(System.Security.Claims.ClaimsPrincipal? user) =>
        user?.Identity?.IsAuthenticated == true
        && user.FindAll(JwtTokenIssuer.MustChangePasswordClaim)
            .Any(c => string.Equals(c.Value, JwtTokenIssuer.MustChangePasswordClaimValue, StringComparison.OrdinalIgnoreCase));
}
