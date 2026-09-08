using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public sealed class AuthRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter auditWriter) : IAuthRepository
{
    private const string TableName = "AppUser";
    private const string PasswordChangedAuditDesc = "PasswordHash (changed by user)";

    public async Task<bool> UpdateUserNameAsync(string userId, string userName, CancellationToken cancellationToken)
    {
        // Only UserName: UserId is the key, roles / IsActive / PasswordHash are out of reach of a self-service edit.
        const string sql = """
            UPDATE AppUser
            SET UserName = @UserName
            WHERE UserId = @UserId;
            """;

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var previous = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT UserName FROM AppUser WHERE UserId = @UserId",
            new { UserId = userId }, transaction, cancellationToken: cancellationToken));
        if (previous is null)
        {
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { UserId = userId, UserName = userName }, transaction, cancellationToken: cancellationToken));

        var description = string.Equals(previous, userName, StringComparison.Ordinal)
            ? "(no changes)"
            : $"{nameof(AppUserCredential.UserName)} (profile)";
        await auditWriter.WriteAsync(connection, TableName, userId, RowAuditWriter.Update, description, cancellationToken, transaction);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UpdatePasswordAsync(string userId, string passwordHash, DateTime passwordUpdatedTime, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE AppUser
            SET PasswordHash = @PasswordHash,
                PasswordUpdatedTime = @PasswordUpdatedTime
            WHERE UserId = @UserId;
            """;

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM AppUser WHERE UserId = @UserId",
            new { UserId = userId }, transaction, cancellationToken: cancellationToken));
        if (exists == 0)
        {
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { UserId = userId, PasswordHash = passwordHash, PasswordUpdatedTime = passwordUpdatedTime },
            transaction, cancellationToken: cancellationToken));

        await auditWriter.WriteAsync(connection, TableName, userId, RowAuditWriter.Update,
            PasswordChangedAuditDesc, cancellationToken, transaction);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<AppUserCredential?> GetCredentialAsync(string userId, CancellationToken cancellationToken)
    {
        // The only SELECT in the code base that reads PasswordHash. The row is compared in memory by the controller
        // so that UserId matching is exact (ordinal) regardless of the column collation.
        const string sql = """
            SELECT UserId, UserName, IsActive, PasswordHash
            FROM AppUser
            WHERE UserId = @UserId
            """;

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<AppUserCredential>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task<PasswordStamp?> GetPasswordStampAsync(string userId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var stamp = await connection.QuerySingleOrDefaultAsync<PasswordStamp>(new CommandDefinition(
            "SELECT PasswordUpdatedTime FROM AppUser WHERE UserId = @UserId",
            new { UserId = userId }, cancellationToken: cancellationToken));

        if (stamp?.PasswordUpdatedTime is { } time)
        {
            // The column is written with GETUTCDATE() / TimeProvider.GetUtcNow(); Dapper hands it back as Unspecified.
            stamp.PasswordUpdatedTime = DateTime.SpecifyKind(time, DateTimeKind.Utc);
        }

        return stamp;
    }

    public async Task<IReadOnlyList<string>> GetRoleIdsAsync(string userId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var ids = await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT RoleId FROM AppUserRole WHERE UserId = @UserId ORDER BY RoleId",
            new { UserId = userId }, cancellationToken: cancellationToken));
        return ids.AsList();
    }

    public async Task<string> GetSymmetricSecurityKeyAsync(CancellationToken cancellationToken) =>
        AppConfigJson.ExtractSymmetricSecurityKey(await GetAppConfigValueAsync(cancellationToken));

    public async Task<string> GetDefaultPasswordAsync(CancellationToken cancellationToken) =>
        AppConfigJson.ExtractDefaultPassword(await GetAppConfigValueAsync(cancellationToken));

    /// <summary>The raw <c>SysConfig.appConfig</c> JSON, or null when the row is missing.</summary>
    private async Task<string?> GetAppConfigValueAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT configValue FROM SysConfig WHERE configKey = @ConfigKey",
            new { ConfigKey = AppConfigJson.ConfigKey }, cancellationToken: cancellationToken));
    }
}
