namespace CMS.API.Infrastructure;

/// <summary>
/// Marks an action that a user who still has to change their password (token carries
/// <see cref="JwtTokenIssuer.MustChangePasswordClaim"/>) may call. Only <c>AuthController.ChangePassword</c>
/// should carry it; <see cref="PasswordChangeRequiredFilter"/> answers 403 everywhere else.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AllowPasswordChangeRequiredAttribute : Attribute
{
}
