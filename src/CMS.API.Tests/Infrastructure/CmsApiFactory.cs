using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CMS.API.Tests.Infrastructure;

/// <summary>
/// Hosts the real <c>Program</c> pipeline (bearer authentication, global authorization, controllers) in-memory
/// with the database-facing repositories replaced by mocks, so end-to-end HTTP behaviour can be asserted
/// without a <c>CMS</c> database. Only the repositories a test hits need a setup.
/// </summary>
public sealed class CmsApiFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "integration-test-symmetric-security-key-0123456789";
    public const string UserId = "helen";
    public const string UserName = "Helen Chen";
    public const string Password = "Welcome123!";
    /// <summary>SysConfig.appConfig.defaultPassword — a login with it gets a token that only opens the password change.</summary>
    public const string DefaultPassword = "Cms@Default2026";

    public Mock<IAuthRepository> AuthRepository { get; } = new(MockBehavior.Strict);
    public Mock<IPublishStatusRepository> PublishStatusRepository { get; } = new(MockBehavior.Strict);
    public Mock<IRowAuditRepository> RowAuditRepository { get; } = new(MockBehavior.Strict);

    /// <summary>Everything the hosted API logged, so a test can assert what reached the server log and what did not.</summary>
    public CapturingLoggerProvider Logs { get; } = new();

    public CmsApiFactory()
    {
        AuthRepository
            .Setup(r => r.GetSymmetricSecurityKeyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(SigningKey);
        AuthRepository
            .Setup(r => r.GetDefaultPasswordAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultPassword);
        AuthRepository
            .Setup(r => r.GetCredentialAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Credential());
        AuthRepository
            .Setup(r => r.GetRoleIdsAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["Admin"]);
        AuthRepository
            .Setup(r => r.UpdateUserNameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        // No password change on record, so every token issued for the user is accepted by the bearer handler.
        AuthRepository
            .Setup(r => r.GetPasswordStampAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordStamp { PasswordUpdatedTime = null });

        PublishStatusRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PublishStatus { Pkid = 1, Description = "草稿", IsDraft = true }]);
    }

    public static AppUserCredential Credential(string password = Password) => new()
    {
        UserId = UserId,
        UserName = UserName,
        IsActive = true,
        PasswordHash = PasswordHasher.Sha256Hex(password)
    };

    /// <summary>A token exactly as <c>POST /api/auth/login</c> would issue it, optionally with a pinned clock or another key.</summary>
    public static string IssueToken(TimeProvider? clock = null, string signingKey = SigningKey, params string[] roles) =>
        new JwtTokenIssuer(clock ?? TimeProvider.System).Issue(Credential(), roles, signingKey);

    /// <summary>A token as issued to a login that used the default password: carries the must-change-password claim.</summary>
    public static string IssueMustChangePasswordToken(params string[] roles) =>
        new JwtTokenIssuer(TimeProvider.System).Issue(Credential(), roles, SigningKey, mustChangePassword: true);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));

        // Runs after Program.cs registered its services, so these replace the Dapper implementations.
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAuthRepository>();
            services.AddSingleton(AuthRepository.Object);

            services.RemoveAll<IPublishStatusRepository>();
            services.AddSingleton(PublishStatusRepository.Object);

            services.RemoveAll<IRowAuditRepository>();
            services.AddSingleton(RowAuditRepository.Object);
        });
    }
}
