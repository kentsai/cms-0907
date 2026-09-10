using System.Security.Claims;
using CMS.API.Controllers;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CMS.API.Tests.Controllers;

/// <summary>
/// Unit tests for <c>POST /api/auth/change-password</c>: the user comes from the principal, the current password
/// gates everything, the new password is policy-checked and confirmed, and only then is the hash written.
/// </summary>
public class AuthControllerChangePasswordTests
{
    private const string CurrentPassword = "Welcome123!";
    private const string ValidNewPassword = "Summer2026!";
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 10, 15, 0, TimeSpan.Zero);

    private readonly Mock<IAuthRepository> _repository = new(MockBehavior.Strict);
    private readonly Mock<IPasswordStampCache> _passwordStamps = new();
    private readonly AuthController _controller;

    /// <summary>The hash handed to <c>UpdatePasswordAsync</c>, captured because hashing is salted and so not reproducible.</summary>
    private string? _writtenHash;

    public AuthControllerChangePasswordTests()
    {
        _controller = new AuthController(
            _repository.Object, Mock.Of<IJwtTokenIssuer>(), new FixedTimeProvider(Now), _passwordStamps.Object,
            NullLogger<AuthController>.Instance);
        SignInAs("helen");
    }

    private void SignInAs(string userId)
    {
        var identity = new ClaimsIdentity([new Claim(JwtTokenIssuer.UserIdClaim, userId)], "Bearer", JwtTokenIssuer.UserIdClaim, ClaimTypes.Role);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private static AppUserCredential StoredUser(string userId = "helen", string password = CurrentPassword) => new()
    {
        UserId = userId,
        UserName = "Helen Chen",
        IsActive = true,
        PasswordHash = PasswordHasher.Hash(password)
    };

    private static ChangePasswordRequest Request(
        string currentPassword = CurrentPassword, string newPassword = ValidNewPassword, string? confirmNewPassword = null) => new()
    {
        CurrentPassword = currentPassword,
        NewPassword = newPassword,
        ConfirmNewPassword = confirmNewPassword ?? newPassword
    };

    private void SetupCredential(AppUserCredential? user, string userId = "helen") =>
        _repository.Setup(r => r.GetCredentialAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

    /// <summary>
    /// Accepts whatever hash the controller produces and records it in <see cref="_writtenHash"/>: every new hash
    /// carries a fresh random salt, so the test cannot predict the string and asserts with
    /// <see cref="PasswordHasher.Verify"/> instead.
    /// </summary>
    private void SetupUpdate(string userId, bool result = true) =>
        _repository
            .Setup(r => r.UpdatePasswordAsync(userId, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback((string _, string hash, DateTime _, CancellationToken _) => _writtenHash = hash)
            .ReturnsAsync(result);

    /// <summary>The stored hash was rewritten and the given plaintext is what now opens the account.</summary>
    private void AssertStoredPasswordIsNow(string password)
    {
        Assert.NotNull(_writtenHash);
        Assert.False(PasswordHasher.IsLegacyFormat(_writtenHash));
        Assert.True(PasswordHasher.Verify(password, _writtenHash, out var needsUpgrade));
        Assert.False(needsUpgrade);
    }

    private static void AssertFieldError(IActionResult result, string field, string message)
    {
        var problem = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        var details = Assert.IsType<ValidationProblemDetails>(problem.Value);
        var error = Assert.Single(details.Errors);
        Assert.Equal(field, error.Key);
        Assert.Contains(message, error.Value);
    }

    private void AssertNothingChanged()
    {
        _repository.Verify(r => r.UpdatePasswordAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _passwordStamps.Verify(c => c.Invalidate(It.IsAny<string>()), Times.Never);
    }

    // ---- success ----

    [Fact]
    public async Task ChangePassword_StoresASaltedHashOfTheNewPassword_AndTheCurrentTime()
    {
        SetupCredential(StoredUser());
        SetupUpdate("helen");

        var result = await _controller.ChangePassword(Request(), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        _repository.Verify(r => r.UpdatePasswordAsync(
            "helen", It.IsAny<string>(), Now.UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
        AssertStoredPasswordIsNow(ValidNewPassword);
        // The old password must no longer open the account.
        Assert.False(PasswordHasher.Verify(CurrentPassword, _writtenHash, out _));
    }

    [Fact]
    public async Task ChangePassword_NeverStoresTheNewPasswordInTheLegacyUnsaltedFormat()
    {
        SetupCredential(StoredUser());
        SetupUpdate("helen");

        await _controller.ChangePassword(Request(), CancellationToken.None);

        Assert.NotNull(_writtenHash);
        Assert.NotEqual(PasswordHasher.Sha256Hex(ValidNewPassword), _writtenHash);
        Assert.StartsWith(PasswordHasher.Pbkdf2Prefix, _writtenHash);
    }

    [Fact]
    public async Task ChangePassword_SaltsEachHash_SoTheSamePasswordStoresDifferentValues()
    {
        SetupCredential(StoredUser());
        SetupUpdate("helen");
        await _controller.ChangePassword(Request(), CancellationToken.None);
        var first = _writtenHash;

        SetupCredential(StoredUser());
        await _controller.ChangePassword(Request(), CancellationToken.None);

        Assert.NotEqual(first, _writtenHash);
        AssertStoredPasswordIsNow(ValidNewPassword);
    }

    [Fact]
    public async Task ChangePassword_InvalidatesTheCachedPasswordStamp_SoTheOldTokenIsRejectedNext()
    {
        SetupCredential(StoredUser());
        SetupUpdate("helen");

        await _controller.ChangePassword(Request(), CancellationToken.None);

        _passwordStamps.Verify(c => c.Invalidate("helen"), Times.Once);
    }

    [Theory]
    [InlineData("Abcdefg1")]   // upper + lower + digit, exactly 8
    [InlineData("abcdefg1!")]  // lower + digit + symbol
    [InlineData("ABCDEFG1!")]  // upper + digit + symbol
    [InlineData("Abcdefg!")]   // upper + lower + symbol
    [InlineData("Ab1!Ab1!")]   // all four
    [InlineData("Abc 123 ~")]  // spaces are allowed and simply do not count
    public async Task ChangePassword_AcceptsPasswordsThatMeetThePolicy(string newPassword)
    {
        SetupCredential(StoredUser());
        SetupUpdate("helen");

        var result = await _controller.ChangePassword(Request(newPassword: newPassword), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task ChangePassword_UsesTheTokenUserId_NotAnythingTheCallerSupplies()
    {
        // The request type has no UserId member at all, so the only possible source is the principal.
        Assert.Null(typeof(ChangePasswordRequest).GetProperty("UserId"));

        SignInAs("mike");
        SetupCredential(StoredUser("mike"), "mike");
        SetupUpdate("mike");

        await _controller.ChangePassword(Request(), CancellationToken.None);

        _repository.Verify(r => r.GetCredentialAsync("mike", It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.UpdatePasswordAsync("mike", It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ChangePassword_MatchesALegacyStoredHashCaseInsensitively()
    {
        // Rows written by the previous system hold an unsalted hex digest, sometimes upper-cased.
        var user = StoredUser();
        user.PasswordHash = PasswordHasher.Sha256Hex(CurrentPassword).ToUpperInvariant();
        SetupCredential(user);
        SetupUpdate("helen");

        var result = await _controller.ChangePassword(Request(), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        // Changing the password is also what retires the legacy format for that user.
        AssertStoredPasswordIsNow(ValidNewPassword);
    }

    // ---- current password ----

    [Fact]
    public async Task ChangePassword_RejectsAWrongCurrentPassword_AndChangesNothing()
    {
        SetupCredential(StoredUser());

        var result = await _controller.ChangePassword(Request(currentPassword: "not-my-password"), CancellationToken.None);

        AssertFieldError(result, nameof(ChangePasswordRequest.CurrentPassword), AuthController.CurrentPasswordIncorrectMessage);
        AssertNothingChanged();
    }

    [Fact]
    public async Task ChangePassword_ChecksTheCurrentPasswordBeforeTheNewOne()
    {
        // Wrong current password + weak new password → only the current-password error, nothing written.
        SetupCredential(StoredUser());

        var result = await _controller.ChangePassword(Request(currentPassword: "wrong", newPassword: "short"), CancellationToken.None);

        AssertFieldError(result, nameof(ChangePasswordRequest.CurrentPassword), AuthController.CurrentPasswordIncorrectMessage);
        AssertNothingChanged();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task ChangePassword_RejectsAMissingCurrentPassword_WithoutReadingTheUser(string? currentPassword)
    {
        var result = await _controller.ChangePassword(Request(currentPassword: currentPassword!), CancellationToken.None);

        AssertFieldError(result, nameof(ChangePasswordRequest.CurrentPassword), AuthController.CurrentPasswordRequiredMessage);
        _repository.VerifyNoOtherCalls();
    }

    // ---- new password policy ----

    [Theory]
    [InlineData("Ab1!")]       // 3 classes but too short
    [InlineData("Abcdef1")]    // 7 characters
    [InlineData("abcdefgh")]   // 1 class
    [InlineData("ABCDEFGH")]   // 1 class
    [InlineData("12345678")]   // 1 class
    [InlineData("!@#$%^&*")]   // 1 class
    [InlineData("abcdefg1")]   // lower + digit only
    [InlineData("Abcdefgh")]   // upper + lower only
    [InlineData("ABCDEFG1")]   // upper + digit only
    [InlineData("abcdefg!")]   // lower + symbol only
    [InlineData("1234567!")]   // digit + symbol only
    [InlineData("abcd 123")]   // whitespace is not a class
    [InlineData("abcd中文123")] // CJK is not a class
    public async Task ChangePassword_RejectsANewPasswordThatFailsThePolicy(string newPassword)
    {
        SetupCredential(StoredUser());

        var result = await _controller.ChangePassword(Request(newPassword: newPassword), CancellationToken.None);

        AssertFieldError(result, nameof(ChangePasswordRequest.NewPassword), PasswordPolicy.Message);
        AssertNothingChanged();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task ChangePassword_RejectsAMissingNewPassword(string? newPassword)
    {
        SetupCredential(StoredUser());

        var result = await _controller.ChangePassword(Request(newPassword: newPassword!, confirmNewPassword: ""), CancellationToken.None);

        AssertFieldError(result, nameof(ChangePasswordRequest.NewPassword), AuthController.NewPasswordRequiredMessage);
        AssertNothingChanged();
    }

    // ---- confirmation ----

    [Theory]
    [InlineData("Summer2026?")]
    [InlineData("summer2026!")]
    [InlineData("Summer2026! ")]
    [InlineData("")]
    [InlineData(null)]
    public async Task ChangePassword_RejectsAMismatchedConfirmation_AndChangesNothing(string? confirmNewPassword)
    {
        SetupCredential(StoredUser());

        var result = await _controller.ChangePassword(
            new ChangePasswordRequest { CurrentPassword = CurrentPassword, NewPassword = ValidNewPassword, ConfirmNewPassword = confirmNewPassword! },
            CancellationToken.None);

        AssertFieldError(result, nameof(ChangePasswordRequest.ConfirmNewPassword), AuthController.ConfirmPasswordMismatchMessage);
        AssertNothingChanged();
    }

    // ---- principal / row state ----

    [Fact]
    public async Task ChangePassword_Returns401_WhenThePrincipalHasNoUserId()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        var result = await _controller.ChangePassword(Request(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ChangePassword_Returns404_WhenTheUserRowIsGone()
    {
        SetupCredential(null);

        var result = await _controller.ChangePassword(Request(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        AssertNothingChanged();
    }

    [Fact]
    public async Task ChangePassword_Returns404_WhenTheRowDisappearsBeforeTheUpdate()
    {
        SetupCredential(StoredUser());
        SetupUpdate("helen", result: false);

        var result = await _controller.ChangePassword(Request(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        _passwordStamps.Verify(c => c.Invalidate(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ChangePassword_RequiresAuthentication()
    {
        // Login is the only anonymous action; change-password must not have opted out of the global filter.
        var method = typeof(AuthController).GetMethod(nameof(AuthController.ChangePassword))!;
        Assert.Empty(method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), inherit: true));
    }
}
