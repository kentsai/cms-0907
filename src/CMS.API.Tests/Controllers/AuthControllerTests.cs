using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CMS.API.Controllers;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace CMS.API.Tests.Controllers;

public class AuthControllerTests
{
    private const string Password = "Welcome123!";
    /// <summary>SysConfig.appConfig.defaultPassword in these tests — deliberately not the user's own password.</summary>
    private const string DefaultPassword = "Cms@Default2026";
    private const string SigningKey = "unit-test-symmetric-security-key-0123456789";
    private static readonly DateTimeOffset IssuedAt = new(2026, 9, 8, 9, 30, 0, TimeSpan.Zero);

    private readonly Mock<IAuthRepository> _repository = new(MockBehavior.Strict);
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        // A real issuer with a pinned clock so the JWT can be decoded and its claims / expiry asserted.
        _controller = new AuthController(_repository.Object, new JwtTokenIssuer(new FixedTimeProvider(IssuedAt)), new FixedTimeProvider(IssuedAt), Mock.Of<IPasswordStampCache>());
    }

    private static AppUserCredential ActiveUser(string userId = "helen", bool isActive = true) => new()
    {
        UserId = userId,
        UserName = "Helen Chen",
        IsActive = isActive,
        PasswordHash = PasswordHasher.Sha256Hex(Password)
    };

    private static LoginRequest Request(string userId = "helen", string password = Password) => new()
    {
        UserId = userId,
        Password = password
    };

    private void SetupSuccessPath(string userId, IReadOnlyList<string> roleIds)
    {
        _repository.Setup(r => r.GetRoleIdsAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(roleIds);
        _repository.Setup(r => r.GetSymmetricSecurityKeyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(SigningKey);
        _repository.Setup(r => r.GetDefaultPasswordAsync(It.IsAny<CancellationToken>())).ReturnsAsync(DefaultPassword);
    }

    private static bool HasMustChangePasswordClaim(JwtSecurityToken jwt) =>
        jwt.Claims.Any(c => c.Type == JwtTokenIssuer.MustChangePasswordClaim
                            && string.Equals(c.Value, JwtTokenIssuer.MustChangePasswordClaimValue, StringComparison.OrdinalIgnoreCase));

    private static JwtSecurityToken DecodeAndValidate(string accessToken)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = JwtTokenIssuer.Issuer,
            ValidateAudience = false,
            ValidateLifetime = false, // the pinned issue time is not "now"; expiry is asserted explicitly
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            RoleClaimType = ClaimTypes.Role
        };

        new JwtSecurityTokenHandler().ValidateToken(accessToken, parameters, out var validated);
        return Assert.IsType<JwtSecurityToken>(validated);
    }

    // ---- success ----

    [Fact]
    public async Task Login_ReturnsOkProfileWithToken_ForValidActiveUser()
    {
        _repository.Setup(r => r.GetCredentialAsync("helen", It.IsAny<CancellationToken>())).ReturnsAsync(ActiveUser());
        SetupSuccessPath("helen", ["Admin", "Editor"]);

        var result = await _controller.Login(Request(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<LoginResponse>(ok.Value);
        Assert.Equal("helen", body.UserId);
        Assert.Equal("Helen Chen", body.UserName);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.Equal(3, body.AccessToken.Split('.').Length); // header.payload.signature
    }

    [Fact]
    public async Task Login_TokenIsSignedWithTheSysConfigKey_AndCarriesUserAndRoleClaims()
    {
        _repository.Setup(r => r.GetCredentialAsync("helen", It.IsAny<CancellationToken>())).ReturnsAsync(ActiveUser());
        SetupSuccessPath("helen", ["Admin", "Editor"]);

        var result = await _controller.Login(Request(), CancellationToken.None);

        var body = Assert.IsType<LoginResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var jwt = DecodeAndValidate(body.AccessToken); // throws if the signature does not match SigningKey

        Assert.Equal("helen", jwt.Subject);
        Assert.Equal("helen", jwt.Claims.Single(c => c.Type == JwtTokenIssuer.UserIdClaim).Value);
        Assert.Equal("Helen Chen", jwt.Claims.Single(c => c.Type == JwtTokenIssuer.UserNameClaim).Value);

        var roles = jwt.Claims.Where(c => c.Type is ClaimTypes.Role or "role").Select(c => c.Value).OrderBy(r => r).ToList();
        Assert.Equal(["Admin", "Editor"], roles);
    }

    [Fact]
    public async Task Login_TokenExpires24HoursAfterIssue()
    {
        _repository.Setup(r => r.GetCredentialAsync("helen", It.IsAny<CancellationToken>())).ReturnsAsync(ActiveUser());
        SetupSuccessPath("helen", []);

        var result = await _controller.Login(Request(), CancellationToken.None);

        var body = Assert.IsType<LoginResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var jwt = DecodeAndValidate(body.AccessToken);

        Assert.Equal(IssuedAt.UtcDateTime, jwt.ValidFrom);
        Assert.Equal(IssuedAt.UtcDateTime.AddHours(24), jwt.ValidTo);
        Assert.Equal(TimeSpan.FromHours(24), jwt.ValidTo - jwt.ValidFrom);
    }

    [Fact]
    public async Task Login_UserWithNoRoles_YieldsTokenWithoutRoleClaims()
    {
        _repository.Setup(r => r.GetCredentialAsync("helen", It.IsAny<CancellationToken>())).ReturnsAsync(ActiveUser());
        SetupSuccessPath("helen", []);

        var result = await _controller.Login(Request(), CancellationToken.None);

        var body = Assert.IsType<LoginResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var jwt = DecodeAndValidate(body.AccessToken);
        Assert.DoesNotContain(jwt.Claims, c => c.Type is ClaimTypes.Role or "role");
    }

    [Fact]
    public async Task Login_AcceptsUpperCaseHexHashStoredInDatabase()
    {
        var user = ActiveUser();
        user.PasswordHash = user.PasswordHash.ToUpperInvariant();
        _repository.Setup(r => r.GetCredentialAsync("helen", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        SetupSuccessPath("helen", []);

        var result = await _controller.Login(Request(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    // ---- 401 paths: identical generic body, no token work performed ----

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenPasswordIsWrong()
    {
        _repository.Setup(r => r.GetCredentialAsync("helen", It.IsAny<CancellationToken>())).ReturnsAsync(ActiveUser());

        var result = await _controller.Login(Request(password: "wrong-password"), CancellationToken.None);

        AssertGenericUnauthorized(result);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenUserIdIsUnknown()
    {
        _repository.Setup(r => r.GetCredentialAsync("ghost", It.IsAny<CancellationToken>())).ReturnsAsync((AppUserCredential?)null);

        var result = await _controller.Login(Request(userId: "ghost"), CancellationToken.None);

        AssertGenericUnauthorized(result);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenUserIsInactive()
    {
        _repository.Setup(r => r.GetCredentialAsync("helen", It.IsAny<CancellationToken>())).ReturnsAsync(ActiveUser(isActive: false));

        var result = await _controller.Login(Request(), CancellationToken.None);

        AssertGenericUnauthorized(result);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenUserIdDiffersOnlyByCase()
    {
        // The DB collation may match "HELEN" to "helen"; the spec requires an exact match.
        _repository.Setup(r => r.GetCredentialAsync("HELEN", It.IsAny<CancellationToken>())).ReturnsAsync(ActiveUser("helen"));

        var result = await _controller.Login(Request(userId: "HELEN"), CancellationToken.None);

        AssertGenericUnauthorized(result);
    }

    [Fact]
    public async Task Login_AllFailureModes_ProduceTheSameBody()
    {
        _repository.Setup(r => r.GetCredentialAsync("ghost", It.IsAny<CancellationToken>())).ReturnsAsync((AppUserCredential?)null);
        _repository.Setup(r => r.GetCredentialAsync("helen", It.IsAny<CancellationToken>())).ReturnsAsync(ActiveUser());
        _repository.Setup(r => r.GetCredentialAsync("sleepy", It.IsAny<CancellationToken>())).ReturnsAsync(ActiveUser("sleepy", isActive: false));

        var unknown = await _controller.Login(Request(userId: "ghost"), CancellationToken.None);
        var wrongPassword = await _controller.Login(Request(password: "nope"), CancellationToken.None);
        var inactive = await _controller.Login(Request(userId: "sleepy"), CancellationToken.None);

        var bodies = new[] { unknown, wrongPassword, inactive }
            .Select(r => JsonSerializer.Serialize(Assert.IsType<UnauthorizedObjectResult>(r.Result).Value))
            .Distinct()
            .ToList();
        Assert.Single(bodies);
    }

    private void AssertGenericUnauthorized(ActionResult<LoginResponse> result)
    {
        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);

        var json = JsonSerializer.Serialize(unauthorized.Value);
        using var body = JsonDocument.Parse(json);
        Assert.Equal(["message"], body.RootElement.EnumerateObject().Select(p => p.Name).ToList());
        Assert.Equal(AuthController.InvalidCredentialsMessage, body.RootElement.GetProperty("message").GetString());
        Assert.DoesNotContain("IsActive", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accessToken", json, StringComparison.OrdinalIgnoreCase);

        _repository.Verify(r => r.GetRoleIdsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.GetSymmetricSecurityKeyAsync(It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.GetDefaultPasswordAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- default password → must change it before anything else ----

    [Fact]
    public async Task Login_WithTheDefaultPassword_FlagsTheResponseAndTheToken()
    {
        var user = ActiveUser();
        user.PasswordHash = PasswordHasher.Sha256Hex(DefaultPassword); // admin-seeded / reset account
        _repository.Setup(r => r.GetCredentialAsync("helen", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        SetupSuccessPath("helen", ["Editor"]);

        var result = await _controller.Login(Request(password: DefaultPassword), CancellationToken.None);

        var body = Assert.IsType<LoginResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.True(body.MustChangePassword);
        Assert.True(HasMustChangePasswordClaim(DecodeAndValidate(body.AccessToken)));
    }

    [Fact]
    public async Task Login_WithAnOwnPassword_IsNotFlagged_AndTheTokenHasNoSuchClaim()
    {
        _repository.Setup(r => r.GetCredentialAsync("helen", It.IsAny<CancellationToken>())).ReturnsAsync(ActiveUser());
        SetupSuccessPath("helen", ["Editor"]);

        var result = await _controller.Login(Request(), CancellationToken.None);

        var body = Assert.IsType<LoginResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.False(body.MustChangePassword);
        var jwt = DecodeAndValidate(body.AccessToken);
        Assert.False(HasMustChangePasswordClaim(jwt));
        Assert.DoesNotContain(jwt.Claims, c => c.Type == JwtTokenIssuer.MustChangePasswordClaim);
    }

    [Fact]
    public async Task Login_DefaultPasswordComparisonIsOrdinal_CaseDiffersMeansNotTheDefault()
    {
        // The stored hash is of the mixed-case variant, so the login succeeds, but it is not the configured default.
        var user = ActiveUser();
        user.PasswordHash = PasswordHasher.Sha256Hex(DefaultPassword.ToUpperInvariant());
        _repository.Setup(r => r.GetCredentialAsync("helen", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        SetupSuccessPath("helen", []);

        var result = await _controller.Login(Request(password: DefaultPassword.ToUpperInvariant()), CancellationToken.None);

        var body = Assert.IsType<LoginResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.False(body.MustChangePassword);
    }

    [Fact]
    public async Task Login_WrongPassword_NeverReadsTheDefaultPassword()
    {
        _repository.Setup(r => r.GetCredentialAsync("helen", It.IsAny<CancellationToken>())).ReturnsAsync(ActiveUser());

        var result = await _controller.Login(Request(password: DefaultPassword), CancellationToken.None);

        AssertGenericUnauthorized(result);
    }

    [Fact]
    public async Task Login_Returns500Problem_WhenTheDefaultPasswordIsUnavailable()
    {
        _repository.Setup(r => r.GetCredentialAsync("helen", It.IsAny<CancellationToken>())).ReturnsAsync(ActiveUser());
        _repository.Setup(r => r.GetRoleIdsAsync("helen", It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _repository.Setup(r => r.GetSymmetricSecurityKeyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(SigningKey);
        _repository.Setup(r => r.GetDefaultPasswordAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AppConfigException("SysConfig 缺少 defaultPassword"));

        var result = await _controller.Login(Request(), CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
        Assert.Equal("SysConfig 缺少 defaultPassword", Assert.IsType<ProblemDetails>(problem.Value).Detail);
    }

    // ---- configuration errors ----

    [Fact]
    public async Task Login_Returns500Problem_WhenSigningKeyIsUnavailable()
    {
        _repository.Setup(r => r.GetCredentialAsync("helen", It.IsAny<CancellationToken>())).ReturnsAsync(ActiveUser());
        _repository.Setup(r => r.GetRoleIdsAsync("helen", It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _repository.Setup(r => r.GetSymmetricSecurityKeyAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AppConfigException("SysConfig 缺少 symmetricSecurityKey"));

        var result = await _controller.Login(Request(), CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("SysConfig 缺少 symmetricSecurityKey", details.Detail);
    }

    [Fact]
    public async Task Login_Returns500Problem_WhenSigningKeyIsTooShortForHs256()
    {
        _repository.Setup(r => r.GetCredentialAsync("helen", It.IsAny<CancellationToken>())).ReturnsAsync(ActiveUser());
        _repository.Setup(r => r.GetRoleIdsAsync("helen", It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _repository.Setup(r => r.GetSymmetricSecurityKeyAsync(It.IsAny<CancellationToken>())).ReturnsAsync("short");
        _repository.Setup(r => r.GetDefaultPasswordAsync(It.IsAny<CancellationToken>())).ReturnsAsync(DefaultPassword);

        var result = await _controller.Login(Request(), CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
    }

    // ---- PasswordHash must never cross the API boundary ----

    [Fact]
    public void LoginResponse_HasNoPasswordMember()
    {
        var names = typeof(LoginResponse).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToList();

        Assert.Equal(["UserId", "UserName", "AccessToken", "MustChangePassword"], names);
        // MustChangePassword is a bool flag; no member can carry the hash or the password itself.
        Assert.DoesNotContain(names, n => n.Contains("Hash", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(typeof(bool), typeof(LoginResponse).GetProperty(nameof(LoginResponse.MustChangePassword))!.PropertyType);
        Assert.All(typeof(LoginResponse).GetProperties().Where(p => p.PropertyType == typeof(string)),
            p => Assert.DoesNotContain("Password", p.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Login_SuccessResponseAndToken_NeverContainThePasswordHash()
    {
        var user = ActiveUser();
        _repository.Setup(r => r.GetCredentialAsync("helen", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        SetupSuccessPath("helen", ["Admin"]);

        var result = await _controller.Login(Request(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var json = JsonSerializer.Serialize(ok.Value, ok.Value!.GetType());
        Assert.DoesNotContain(user.PasswordHash, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Password, json);

        // The JWT payload is only base64url-encoded, so check the decoded claims too.
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(Assert.IsType<LoginResponse>(ok.Value).AccessToken);
        Assert.DoesNotContain(jwt.Claims, c => c.Value.Equals(user.PasswordHash, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(jwt.Claims, c => c.Type.Contains("hash", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(jwt.Claims, c => c.Value.Equals(Password, StringComparison.Ordinal));
    }

    // ---- Request validation ----

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Request_IsValid_WithUserIdAndPassword()
    {
        Assert.Empty(Validate(Request()));
    }

    [Theory]
    [InlineData("", "secret")]
    [InlineData("   ", "secret")]
    [InlineData("helen", "")]
    [InlineData("helen", "   ")]
    public void Request_IsInvalid_WhenUserIdOrPasswordMissing(string userId, string password)
    {
        Assert.NotEmpty(Validate(Request(userId, password)));
    }
}
