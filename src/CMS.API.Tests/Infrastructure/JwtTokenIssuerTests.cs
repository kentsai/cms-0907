using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CMS.API.Infrastructure;
using CMS.API.Models;
using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Tests.Infrastructure;

public class JwtTokenIssuerTests
{
    private const string SigningKey = "another-unit-test-key-that-is-long-enough-for-hs256";
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly JwtTokenIssuer _issuer = new(new FixedTimeProvider(Now));

    private static AppUserCredential User() => new()
    {
        UserId = "miles@uuu.com.tw",
        UserName = "Miles Lin",
        IsActive = true,
        PasswordHash = PasswordHasher.Sha256Hex("irrelevant")
    };

    [Fact]
    public void Issue_ProducesHs256TokenValidatableWithTheSameKey()
    {
        var token = _issuer.Issue(User(), ["Admin"], SigningKey);

        var handler = new JwtSecurityTokenHandler();
        handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidIssuer = JwtTokenIssuer.Issuer,
            ValidateAudience = false,
            ValidateLifetime = false,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey))
        }, out var validated);

        var jwt = Assert.IsType<JwtSecurityToken>(validated);
        Assert.Equal(SecurityAlgorithms.HmacSha256, jwt.Header.Alg);
    }

    [Fact]
    public void Issue_RejectsTokenSignedWithADifferentKey()
    {
        var token = _issuer.Issue(User(), [], SigningKey);

        var handler = new JwtSecurityTokenHandler();
        Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(() => handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("a-completely-different-key-of-sufficient-length"))
        }, out _));
    }

    [Fact]
    public void Issue_AddsUserIdUserNameAndOneRoleClaimPerRoleId()
    {
        var token = _issuer.Issue(User(), ["Admin", "Editor", "Viewer"], SigningKey);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("miles@uuu.com.tw", jwt.Subject);
        Assert.Equal("miles@uuu.com.tw", jwt.Claims.Single(c => c.Type == JwtTokenIssuer.UserIdClaim).Value);
        Assert.Equal("Miles Lin", jwt.Claims.Single(c => c.Type == JwtTokenIssuer.UserNameClaim).Value);

        // JwtSecurityTokenHandler writes ClaimTypes.Role as the short "role" name.
        var roles = jwt.Claims.Where(c => c.Type is "role" or ClaimTypes.Role).Select(c => c.Value).ToList();
        Assert.Equal(["Admin", "Editor", "Viewer"], roles);
    }

    [Fact]
    public void Issue_AddsTheMustChangePasswordClaim_OnlyWhenAsked()
    {
        var plain = new JwtSecurityTokenHandler().ReadJwtToken(_issuer.Issue(User(), ["Admin"], SigningKey));
        var flagged = new JwtSecurityTokenHandler().ReadJwtToken(_issuer.Issue(User(), ["Admin"], SigningKey, mustChangePassword: true));

        Assert.DoesNotContain(plain.Claims, c => c.Type == JwtTokenIssuer.MustChangePasswordClaim);

        var claim = Assert.Single(flagged.Claims, c => c.Type == JwtTokenIssuer.MustChangePasswordClaim);
        Assert.Equal(JwtTokenIssuer.MustChangePasswordClaimValue, claim.Value, ignoreCase: true);
        // Everything else is unchanged: same subject, same roles.
        Assert.Equal(plain.Subject, flagged.Subject);
        Assert.Equal(
            plain.Claims.Where(c => c.Type is "role" or ClaimTypes.Role).Select(c => c.Value),
            flagged.Claims.Where(c => c.Type is "role" or ClaimTypes.Role).Select(c => c.Value));
    }

    [Fact]
    public void Issue_SetsNotBeforeToNow_AndExpiryTo24HoursLater()
    {
        var token = _issuer.Issue(User(), [], SigningKey);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(Now.UtcDateTime, jwt.ValidFrom);
        Assert.Equal(Now.UtcDateTime.AddHours(24), jwt.ValidTo);
        Assert.Equal(JwtTokenIssuer.TokenLifetime, jwt.ValidTo - jwt.ValidFrom);
    }

    [Fact]
    public void Issue_UsesTheIssuerConstant()
    {
        var token = _issuer.Issue(User(), [], SigningKey);

        Assert.Equal(JwtTokenIssuer.Issuer, new JwtSecurityTokenHandler().ReadJwtToken(token).Issuer);
    }

    [Fact]
    public void Issue_NeverEmitsThePasswordHash()
    {
        var user = User();
        var token = _issuer.Issue(user, ["Admin"], SigningKey);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.DoesNotContain(jwt.Claims, c => c.Value.Equals(user.PasswordHash, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(jwt.Claims, c => c.Type.Contains("password", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("exactly-thirty-one-bytes-long!!")]
    public void Issue_ThrowsAppConfigException_WhenKeyIsShorterThan32Bytes(string key)
    {
        var ex = Assert.Throws<AppConfigException>(() => _issuer.Issue(User(), [], key));

        Assert.Contains(AppConfigJson.SymmetricSecurityKeyProperty, ex.Message);
    }

    [Fact]
    public void Issue_Accepts32ByteKey()
    {
        var token = _issuer.Issue(User(), [], "exactly-thirty-two-bytes-long!!!");

        Assert.Equal(3, token.Split('.').Length);
    }
}
