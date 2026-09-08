using System.Data.Common;
using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CMS.API.Repositories;

public sealed class AppRoleRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter auditWriter)
    : IAppRoleRepository
{
    private const string TableName = "AppRole";
    private const int SqlForeignKeyViolation = 547;

    private const string SelectColumns = """
        SELECT r.pkid, r.RoleId, r.RoleName, r.PermissionLevel, r.Description,
               (SELECT COUNT(*) FROM AppUserRole ur WHERE ur.RoleId = r.RoleId) AS UserCount
        FROM AppRole r
        """;

    public async Task<IReadOnlyList<AppRole>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AppRole>(
            new CommandDefinition(SelectColumns + " ORDER BY r.RoleId ASC", cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<AppRole>> QueryAsync(AppRoleQuery query, CancellationToken cancellationToken)
    {
        const string sql = SelectColumns + """

            WHERE (@Keyword IS NULL OR r.RoleId LIKE '%' + @Keyword + '%' OR r.RoleName LIKE '%' + @Keyword + '%')
              AND (@PermissionLevel IS NULL OR r.PermissionLevel = @PermissionLevel)
              AND (@UserId IS NULL OR EXISTS (SELECT 1 FROM AppUserRole ur WHERE ur.RoleId = r.RoleId AND ur.UserId = @UserId))
            ORDER BY r.RoleId ASC
            """;

        var parameters = new
        {
            Keyword = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim(),
            query.PermissionLevel,
            UserId = string.IsNullOrWhiteSpace(query.UserId) ? null : query.UserId.Trim()
        };

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AppRole>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<AppRole?> GetByIdAsync(string roleId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await GetByIdAsync(connection, roleId, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string roleId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM AppRole WHERE RoleId = @RoleId) THEN 1 ELSE 0 END",
            new { RoleId = roleId },
            cancellationToken: cancellationToken));
    }

    public async Task<int> CreateAsync(AppRoleRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO AppRole (RoleId, RoleName, PermissionLevel, Description)
            VALUES (@RoleId, @RoleName, @PermissionLevel, @Description);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """;

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var pkid = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, ScalarParameters(request), transaction, cancellationToken: cancellationToken));

        await SyncUsersAsync(connection, transaction, request.RoleId, request.UserIds, cancellationToken);

        // PrimaryKeyValues is RoleId ([AuditKey]), ActionDesc the RoleName.
        var created = await GetByIdAsync(connection, request.RoleId, cancellationToken, transaction)
            ?? throw new InvalidOperationException($"{TableName} '{request.RoleId}' was not found after INSERT.");
        await auditWriter.LogInsertAsync(connection, TableName, created, cancellationToken, transaction);

        await transaction.CommitAsync(cancellationToken);
        return pkid;
    }

    public async Task<bool> UpdateAsync(AppRoleRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE AppRole
            SET RoleName = @RoleName,
                PermissionLevel = @PermissionLevel,
                Description = @Description
            WHERE RoleId = @RoleId;
            """;

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var existing = await GetByIdAsync(connection, request.RoleId, cancellationToken, transaction);
        if (existing is null)
        {
            return false;
        }

        await connection.ExecuteAsync(
            new CommandDefinition(sql, ScalarParameters(request), transaction, cancellationToken: cancellationToken));

        await SyncUsersAsync(connection, transaction, request.RoleId, request.UserIds, cancellationToken);

        // Both snapshots carry the sorted UserIds, so the diff covers the scalar columns and the membership set
        // (UserCount is [AuditIgnore]d on the model).
        var updated = await GetByIdAsync(connection, request.RoleId, cancellationToken, transaction)
            ?? throw new InvalidOperationException($"{TableName} '{request.RoleId}' was not found after UPDATE.");
        await auditWriter.LogUpdateAsync(connection, TableName, existing, updated, cancellationToken, transaction);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(string roleId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var existing = await GetByIdAsync(connection, roleId, cancellationToken, transaction);
        if (existing is null)
        {
            return false;
        }

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM AppUserRole WHERE RoleId = @RoleId; DELETE FROM AppRole WHERE RoleId = @RoleId;",
                new { RoleId = roleId }, transaction, cancellationToken: cancellationToken));
        }
        catch (SqlException ex) when (ex.Number == SqlForeignKeyViolation)
        {
            throw new EntityInUseException($"角色「{existing.RoleName}」仍被其他資料使用，無法刪除。");
        }

        await auditWriter.LogDeleteAsync(connection, TableName, existing, cancellationToken, transaction);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<StringLookupItem>> GetLookupAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<StringLookupItem>(new CommandDefinition(
            "SELECT RoleId AS Id, RoleName AS Label FROM AppRole ORDER BY RoleId ASC",
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    // ---- helpers ----

    private static object ScalarParameters(AppRoleRequest request) => new
    {
        RoleId = request.RoleId.Trim(),
        RoleName = request.RoleName.Trim(),
        request.PermissionLevel,
        Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim()
    };

    /// <summary>The role with its sorted <see cref="AppRole.UserIds"/>, or null.</summary>
    private static async Task<AppRole?> GetByIdAsync(
        DbConnection connection, string roleId, CancellationToken cancellationToken, DbTransaction? transaction = null)
    {
        var role = await connection.QuerySingleOrDefaultAsync<AppRole>(new CommandDefinition(
            SelectColumns + " WHERE r.RoleId = @RoleId", new { RoleId = roleId }, transaction, cancellationToken: cancellationToken));
        if (role is null)
        {
            return null;
        }

        role.UserIds = await GetUserIdsAsync(connection, roleId, cancellationToken, transaction);
        return role;
    }

    private static async Task<List<string>> GetUserIdsAsync(
        DbConnection connection, string roleId, CancellationToken cancellationToken, DbTransaction? transaction = null)
    {
        var ids = await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT UserId FROM AppUserRole WHERE RoleId = @RoleId ORDER BY UserId",
            new { RoleId = roleId }, transaction, cancellationToken: cancellationToken));
        return ids.AsList();
    }

    /// <summary>Delete-then-reinsert sync of <c>AppUserRole</c> for one role.</summary>
    private static async Task SyncUsersAsync(
        DbConnection connection, DbTransaction transaction, string roleId, IEnumerable<string> userIds, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM AppUserRole WHERE RoleId = @RoleId",
            new { RoleId = roleId }, transaction, cancellationToken: cancellationToken));

        var rows = Normalize(userIds).Select(userId => new { UserId = userId, RoleId = roleId }).ToList();
        if (rows.Count == 0)
        {
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO AppUserRole (UserId, RoleId) VALUES (@UserId, @RoleId)",
            rows, transaction, cancellationToken: cancellationToken));
    }

    private static List<string> Normalize(IEnumerable<string> ids) =>
        ids.Where(id => !string.IsNullOrWhiteSpace(id))
           .Select(id => id.Trim())
           .Distinct(StringComparer.OrdinalIgnoreCase)
           .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
           .ToList();
}
