namespace CMS.API.Models;

/// <summary>
/// Profile returned by a successful login. Deliberately has no password member of any kind —
/// <c>PasswordHash</c> stays inside the backend (see <see cref="AppUserCredential"/>).
/// </summary>
public class LoginResponse
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;

    /// <summary>Signed JWT (HS256) valid for <see cref="Infrastructure.JwtTokenIssuer.TokenLifetime"/>.</summary>
    public string AccessToken { get; set; } = string.Empty;
}
