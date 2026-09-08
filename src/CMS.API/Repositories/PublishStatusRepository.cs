using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CMS.API.Repositories;

public sealed class PublishStatusRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter auditWriter)
    : IPublishStatusRepository
{
    private const string TableName = "PublishStatus";
    private const int SqlForeignKeyViolation = 547;

    private const string SelectColumns = """
        SELECT pkid, Description, IsDraft, IsPublished, IsDiscontinued
        FROM PublishStatus
        """;

    public async Task<IReadOnlyList<PublishStatus>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PublishStatus>(
            new CommandDefinition(SelectColumns + " ORDER BY pkid ASC", cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<PublishStatus>> QueryAsync(PublishStatusQuery query, CancellationToken cancellationToken)
    {
        const string sql = SelectColumns + """

            WHERE (@Keyword IS NULL OR Description LIKE '%' + @Keyword + '%')
              AND (@IsDraft IS NULL OR IsDraft = @IsDraft)
              AND (@IsPublished IS NULL OR IsPublished = @IsPublished)
              AND (@IsDiscontinued IS NULL OR IsDiscontinued = @IsDiscontinued)
            ORDER BY pkid ASC
            """;

        var parameters = new
        {
            Keyword = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim(),
            query.IsDraft,
            query.IsPublished,
            query.IsDiscontinued
        };

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PublishStatus>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<PublishStatus?> GetByIdAsync(byte pkid, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await GetByIdAsync(connection, pkid, cancellationToken);
    }

    public async Task<bool> ExistsAsync(byte pkid, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM PublishStatus WHERE pkid = @Pkid) THEN 1 ELSE 0 END",
            new { Pkid = pkid },
            cancellationToken: cancellationToken));
    }

    public async Task<byte> CreateAsync(PublishStatusRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO PublishStatus (pkid, Description, IsDraft, IsPublished, IsDiscontinued)
            VALUES (@Pkid, @Description, @IsDraft, @IsPublished, @IsDiscontinued);
            """;

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(sql, request, transaction, cancellationToken: cancellationToken));

        var created = await GetByIdAsync(connection, request.Pkid, cancellationToken, transaction)
            ?? throw new InvalidOperationException($"{TableName} {request.Pkid} was not found after INSERT.");
        await auditWriter.LogInsertAsync(connection, TableName, created, cancellationToken, transaction);

        await transaction.CommitAsync(cancellationToken);
        return request.Pkid;
    }

    public async Task<bool> UpdateAsync(PublishStatusRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE PublishStatus
            SET Description = @Description,
                IsDraft = @IsDraft,
                IsPublished = @IsPublished,
                IsDiscontinued = @IsDiscontinued
            WHERE pkid = @Pkid;
            """;

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var existing = await GetByIdAsync(connection, request.Pkid, cancellationToken, transaction);
        if (existing is null)
        {
            return false;
        }

        var affected = await connection.ExecuteAsync(new CommandDefinition(sql, request, transaction, cancellationToken: cancellationToken));
        if (affected == 0)
        {
            return false;
        }

        var updated = await GetByIdAsync(connection, request.Pkid, cancellationToken, transaction)
            ?? throw new InvalidOperationException($"{TableName} {request.Pkid} was not found after UPDATE.");
        await auditWriter.LogUpdateAsync(connection, TableName, existing, updated, cancellationToken, transaction);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(byte pkid, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var existing = await GetByIdAsync(connection, pkid, cancellationToken, transaction);
        if (existing is null)
        {
            return false;
        }

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM PublishStatus WHERE pkid = @Pkid", new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));
        }
        catch (SqlException ex) when (ex.Number == SqlForeignKeyViolation)
        {
            throw new EntityInUseException($"發布狀態 {pkid}「{existing.Description}」仍被其他資料使用，無法刪除。");
        }

        await auditWriter.LogDeleteAsync(connection, TableName, existing, cancellationToken, transaction);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<LookupItem>> GetLookupAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<LookupItem>(new CommandDefinition(
            "SELECT CAST(pkid AS int) AS Pkid, Description AS Label FROM PublishStatus ORDER BY pkid ASC",
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    private static Task<PublishStatus?> GetByIdAsync(
        System.Data.Common.DbConnection connection,
        byte pkid,
        CancellationToken cancellationToken,
        System.Data.Common.DbTransaction? transaction = null)
    {
        return connection.QuerySingleOrDefaultAsync<PublishStatus>(new CommandDefinition(
            SelectColumns + " WHERE pkid = @Pkid", new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));
    }
}
