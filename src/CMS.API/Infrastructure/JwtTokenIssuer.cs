using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CMS.API.Models;
using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Infrastructure;

public interface IJwtTokenIssuer
{
    /// <summary>
    /// Signs an HS256 access token for <paramref name="user"/> carrying the UserId, UserName and one role claim per
    /// <paramref name="roleIds"/> entry, expiring <see cref="JwtTokenIssuer.TokenLifetime"/> after issue.
    /// Throws <see cref="AppConfigException"/> when <paramref name="symmetricSecurityKey"/> is too short for HS256.
    /// </summary>
    string Issue(AppUserCredential user, IReadOnlyList<string> roleIds, string symmetricSecurityKey);
}

/// <summary>
/// Builds the JWT returned by <c>POST /api/auth/login</c>. The signing secret is supplied per call (it lives in
/// <c>SysConfig.appConfig.symmetricSecurityKey</c>); the clock is injectable for tests.
/// </summary>
public sealed class JwtTokenIssuer(TimeProvider timeProvider) : IJwtTokenIssuer
{
    public const string Issuer = "CMS.API";
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);

    /// <summary>Claim type holding <c>AppUser.UserId</c> (also emitted as <c>sub</c>).</summary>
    public const string UserIdClaim = "userId";

    /// <summary>Claim type holding <c>AppUser.UserName</c>.</summary>
    public const string UserNameClaim = "userName";

    /// <summary>HS256 needs at least 256 bits of key material, otherwise Microsoft.IdentityModel throws IDX10653.</summary>
    private const int MinimumKeyBytes = 32;

    public string Issue(AppUserCredential user, IReadOnlyList<string> roleIds, string symmetricSecurityKey)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(roleIds);

        var keyBytes = Encoding.UTF8.GetBytes(symmetricSecurityKey ?? string.Empty);
        if (keyBytes.Length < MinimumKeyBytes)
        {
            throw new AppConfigException(
                $"SysConfig「{AppConfigJson.ConfigKey}」的「{AppConfigJson.SymmetricSecurityKeyProperty}」長度不足（至少 {MinimumKeyBytes} 個位元組），無法簽發登入權杖。");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            // Explicit iat (the constructor below only writes nbf/exp): the bearer handler compares it with
            // AppUser.PasswordUpdatedTime to sign old sessions out after a password change.
            new(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(now).ToString(), ClaimValueTypes.Integer64),
            new(UserIdClaim, user.UserId),
            new(UserNameClaim, user.UserName),
        };
        claims.AddRange(roleIds.Select(roleId => new Claim(ClaimTypes.Role, roleId)));

        var credentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: null,
            claims: claims,
            notBefore: now,
            expires: now.Add(TokenLifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
