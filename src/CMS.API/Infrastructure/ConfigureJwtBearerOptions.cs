using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Infrastructure;

/// <summary>
/// Configures the <c>Bearer</c> scheme to accept the tokens <see cref="JwtTokenIssuer"/> issues: HS256, issuer
/// <see cref="JwtTokenIssuer.Issuer"/>, no audience, signature checked against the key from SysConfig
/// (<see cref="ISigningKeyCache"/>). <c>User.Identity.Name</c> becomes the <c>userId</c> claim, so
/// <see cref="RowAuditWriter"/> records the signed-in user; <c>User.IsInRole</c> reads the <c>role</c> claims.
/// After the signature and lifetime pass, a token issued before the user's <c>PasswordUpdatedTime</c> is rejected
/// (<see cref="IPasswordStampCache"/>), so changing a password forces a fresh login everywhere.
/// </summary>
public sealed class ConfigureJwtBearerOptions(
    ISigningKeyCache signingKeys,
    IPasswordStampCache passwordStamps,
    ILogger<ConfigureJwtBearerOptions> logger) : IConfigureNamedOptions<JwtBearerOptions>
{
    /// <summary>Tolerance for clock drift between the issuing and validating process (both are this API).</summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(1);

    public const string PasswordChangedFailureMessage = "密碼已變更，請重新登入。";
    public const string UnknownUserFailureMessage = "使用者不存在，請重新登入。";

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name == JwtBearerDefaults.AuthenticationScheme)
        {
            Configure(options);
        }
    }

    public void Configure(JwtBearerOptions options)
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = JwtTokenIssuer.Issuer,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = ClockSkew,
            ValidateIssuerSigningKey = true,
            // Synchronous by contract; the async load happens in OnMessageReceived below.
            IssuerSigningKeyResolver = (_, _, _, _) => signingKeys.CurrentKeys,
            NameClaimType = JwtTokenIssuer.UserIdClaim,
            RoleClaimType = ClaimTypes.Role
        };

        options.Events = new JwtBearerEvents
        {
            // Runs before the token is validated on every request that reaches the handler.
            OnMessageReceived = context => signingKeys.RefreshAsync(context.HttpContext.RequestAborted),
            OnTokenValidated = RejectIfIssuedBeforePasswordChangeAsync
        };
    }

    private async Task RejectIfIssuedBeforePasswordChangeAsync(TokenValidatedContext context)
    {
        var userId = context.Principal?.FindFirst(JwtTokenIssuer.UserIdClaim)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            context.Fail(UnknownUserFailureMessage);
            return;
        }

        try
        {
            var stamp = await passwordStamps.GetAsync(userId, context.HttpContext.RequestAborted);
            if (stamp is null)
            {
                context.Fail(UnknownUserFailureMessage);
                return;
            }

            if (PasswordStampCache.IsIssuedBeforePasswordChange(IssuedAt(context.SecurityToken), stamp.PasswordUpdatedTime))
            {
                context.Fail(PasswordChangedFailureMessage);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Transient (typically the database): the signature and lifetime already passed, so keep the session
            // rather than signing everyone out during an outage; the check runs again on the next request.
            logger.LogError(ex, "讀取使用者密碼更新時間失敗，本次略過「密碼已變更」檢查。");
        }
    }

    /// <summary>
    /// <c>iat</c> of the validated token, whichever handler produced it (the default <see cref="JsonWebToken"/>, or
    /// <see cref="JwtSecurityToken"/> when legacy validators are enabled); falls back to <c>nbf</c>, which
    /// <see cref="JwtTokenIssuer"/> sets to the same instant.
    /// </summary>
    private static DateTime IssuedAt(SecurityToken token)
    {
        var (issuedAt, validFrom) = token switch
        {
            JsonWebToken jwt => (jwt.IssuedAt, jwt.ValidFrom),
            JwtSecurityToken jwt => (jwt.IssuedAt, jwt.ValidFrom),
            _ => (DateTime.MinValue, token.ValidFrom)
        };

        var value = issuedAt == DateTime.MinValue ? validFrom : issuedAt;
        return DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
