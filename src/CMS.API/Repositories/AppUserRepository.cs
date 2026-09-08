using System.Data.Common;
using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CMS.API.Repositories;

public sealed class AppUserRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter auditWriter)
    : IAppUserRepository
{
    private const string TableName = "AppUser";
    private const int SqlForeignKeyViolation = 547;
    private const string PasswordResetAuditDesc = "PasswordHash (reset to default)";

    // PasswordHash is never selected.
    private const string SelectColumns = """
        SELECT u.pkid, u.UserId, u.UserName, u.IsActive, u.PasswordUpdatedTime,
               (SELECT COUNT(*) FROM AppUserRole ur WHERE ur.UserId = u.UserId) AS RoleCount
        FROM AppUser u
        """;

    public async Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AppUser>(
            new CommandDefinition(SelectColumns + " ORDER BY u.UserId ASC", cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<AppUser>> QueryAsync(AppUserQuery query, CancellationToken cancellationToken)
    {
        const string sql = SelectColumns + """

            WHERE (@Keyword IS NULL OR u.UserId LIKE '%' + @Keyword + '%' OR u.UserName LIKE '%' + @Keyword + '%')
              AND (@IsActive IS NULL OR u.IsActive = @IsActive)
              AND (@RoleId IS NULL OR EXISTS (SELECT 1 FROM AppUserRole ur WHERE ur.UserId = u.UserId AND ur.RoleId = @RoleId))
            ORDER BY u.UserId ASC
            """;

        var parameters = new
        {
            Keyword = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim(),
            query.IsActive,
            RoleId = string.IsNullOrWhiteSpace(query.RoleId) ? null : query.RoleId.Trim()
        };

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AppUser>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<AppUser?> GetByIdAsync(string userId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await GetByIdAsync(connection, userId, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string userId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM AppUser WHERE UserId = @UserId) THEN 1 ELSE 0 END",
            new { UserId = userId },
            cancellationToken: cancellationToken));
    }

    public async Task<int> CreateAsync(AppUserRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO AppUser (UserId, UserName, IsActive, PasswordHash, PasswordUpdatedTime)
            VALUES (@UserId, @UserName, @IsActive, @PasswordHash, GETUTCDATE());
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """;

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var scalars = ScalarParameters(request);
        var passwordHash = await GetDefaultPasswordHashAsync(connection, transaction, cancellationToken);
        var parameters = new { scalars.UserId, scalars.UserName, scalars.IsActive, PasswordHash = passwordHash };

        var pkid = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));

        await SyncRolesAsync(connection, transaction, scalars.UserId, request.RoleIds, cancellationToken);

        // PrimaryKeyValues is UserId ([AuditKey]), ActionDesc the UserName.
        var created = await GetByIdAsync(connection, scalars.UserId, cancellationToken, transaction)
            ?? throw new InvalidOperationException($"{TableName} '{scalars.UserId}' was not found after INSERT.");
        await auditWriter.LogInsertAsync(connection, TableName, created, cancellationToken, transaction);

        await transaction.CommitAsync(cancellationToken);
        return pkid;
    }

    public async Task<bool> UpdateAsync(AppUserRequest request, CancellationToken cancellationToken)
    {
        // PasswordHash / PasswordUpdatedTime are deliberately absent.
        const string sql = """
            UPDATE AppUser
            SET UserName = @UserName,
                IsActive = @IsActive
            WHERE UserId = @UserId;
            """;

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var scalars = ScalarParameters(request);
        var existing = await GetByIdAsync(connection, scalars.UserId, cancellationToken, transaction);
        if (existing is null)
        {
            return false;
        }

        await connection.ExecuteAsync(
            new CommandDefinition(sql, scalars, transaction, cancellationToken: cancellationToken));

        await SyncRolesAsync(connection, transaction, scalars.UserId, request.RoleIds, cancellationToken);

        // Both snapshots carry the sorted RoleIds, so the diff covers UserName / IsActive and the membership set
        // (RoleCount is [AuditIgnore]d on the model; PasswordHash never appears in the model at all).
        var updated = await GetByIdAsync(connection, scalars.UserId, cancellationToken, transaction)
            ?? throw new InvalidOperationException($"{TableName} '{scalars.UserId}' was not found after UPDATE.");
        await auditWriter.LogUpdateAsync(connection, TableName, existing, updated, cancellationToken, transaction);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(string userId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var existing = await GetByIdAsync(connection, userId, cancellationToken, transaction);
        if (existing is null)
        {
            return false;
        }

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM AppUserRole WHERE UserId = @UserId; DELETE FROM AppUser WHERE UserId = @UserId;",
                new { UserId = userId }, transaction, cancellationToken: cancellationToken));
        }
        catch (SqlException ex) when (ex.Number == SqlForeignKeyViolation)
        {
            throw new EntityInUseException($"使用者「{existing.UserName}」仍被其他資料使用，無法刪除。");
        }

        await auditWriter.LogDeleteAsync(connection, TableName, existing, cancellationToken, transaction);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ResetPasswordAsync(string userId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE AppUser
            SET PasswordHash = @PasswordHash,
                PasswordUpdatedTime = GETUTCDATE()
            WHERE UserId = @UserId;
            """;

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var existing = await GetByIdAsync(connection, userId, cancellationToken, transaction);
        if (existing is null)
        {
            return false;
        }

        var passwordHash = await GetDefaultPasswordHashAsync(connection, transaction, cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { UserId = userId, PasswordHash = passwordHash }, transaction, cancellationToken: cancellationToken));

        await auditWriter.WriteAsync(connection, TableName, userId, RowAuditWriter.Update,
            PasswordResetAuditDesc, cancellationToken, transaction);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<StringLookupItem>> GetLookupAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT UserId AS Id, UserName + ' (' + UserId + ')' AS Label
            FROM AppUser
            ORDER BY UserName ASC, UserId ASC
            """;

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<StringLookupItem>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    // ---- helpers ----

    /// <summary>Reads SysConfig.appConfig on the caller's transaction and hashes its defaultPassword.</summary>
    private static async Task<string> GetDefaultPasswordHashAsync(
        DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
    {
        var configValue = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT configValue FROM SysConfig WHERE configKey = @ConfigKey",
            new { ConfigKey = AppConfigJson.ConfigKey }, transaction, cancellationToken: cancellationToken));

        var defaultPassword = AppConfigJson.ExtractDefaultPassword(configValue);
        return PasswordHasher.Sha256Hex(defaultPassword);
    }

    private sealed class UserScalars
    {
        public string UserId { get; init; } = string.Empty;
        public string UserName { get; init; } = string.Empty;
        public bool IsActive { get; init; }
    }

    private static UserScalars ScalarParameters(AppUserRequest request) => new()
    {
        UserId = request.UserId.Trim(),
        UserName = request.UserName.Trim(),
        IsActive = request.IsActive
    };

    /// <summary>The user with their sorted <see cref="AppUser.RoleIds"/>, or null. Never reads PasswordHash.</summary>
    private static async Task<AppUser?> GetByIdAsync(
        DbConnection connection, string userId, CancellationToken cancellationToken, DbTransaction? transaction = null)
    {
        var user = await connection.QuerySingleOrDefaultAsync<AppUser>(new CommandDefinition(
            SelectColumns + " WHERE u.UserId = @UserId", new { UserId = userId }, transaction, cancellationToken: cancellationToken));
        if (user is null)
        {
            return null;
        }

        user.RoleIds = await GetRoleIdsAsync(connection, userId, cancellationToken, transaction);
        return user;
    }

    private static async Task<List<string>> GetRoleIdsAsync(
        DbConnection connection, string userId, CancellationToken cancellationToken, DbTransaction? transaction = null)
    {
        var ids = await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT RoleId FROM AppUserRole WHERE UserId = @UserId ORDER BY RoleId",
            new { UserId = userId }, transaction, cancellationToken: cancellationToken));
        return ids.AsList();
    }

    /// <summary>Delete-then-reinsert sync of <c>AppUserRole</c> for one user.</summary>
    private static async Task SyncRolesAsync(
        DbConnection connection, DbTransaction transaction, string userId, IEnumerable<string> roleIds, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM AppUserRole WHERE UserId = @UserId",
            new { UserId = userId }, transaction, cancellationToken: cancellationToken));

        var rows = Normalize(roleIds).Select(roleId => new { UserId = userId, RoleId = roleId }).ToList();
        if (rows.Count == 0)
        {
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO AppUserRole (UserId, RoleId) VALUES (@UserId, @RoleId)",
            rows, transaction, cancellationToken: cancellationToken));
    }

    private static List<string> Normalize(IEnumerable<string>? ids) =>
        (ids ?? [])
           .Where(id => !string.IsNullOrWhiteSpace(id))
           .Select(id => id.Trim())
           .Distinct(StringComparer.OrdinalIgnoreCase)
           .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
           .ToList();
}
