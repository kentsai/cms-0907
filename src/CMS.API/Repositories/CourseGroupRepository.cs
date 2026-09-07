using System.Data.Common;
using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CMS.API.Repositories;

public sealed class CourseGroupRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter auditWriter)
    : ICourseGroupRepository
{
    private const string TableName = "CourseGroup";
    private const int SqlForeignKeyViolation = 547;

    private const string SelectColumns = """
        SELECT pkid, Description
        FROM CourseGroup
        """;

    private const string DefaultOrder = " ORDER BY pkid DESC";

    public async Task<IReadOnlyList<CourseGroup>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<CourseGroup>(
            new CommandDefinition(SelectColumns + DefaultOrder, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<CourseGroup>> QueryAsync(CourseGroupQuery query, CancellationToken cancellationToken)
    {
        const string sql = SelectColumns + """

            WHERE (@Keyword IS NULL OR Description LIKE '%' + @Keyword + '%')
            """ + DefaultOrder;

        var parameters = new
        {
            Keyword = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim()
        };

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<CourseGroup>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<CourseGroup?> GetByIdAsync(short pkid, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await GetByIdAsync(connection, pkid, cancellationToken);
    }

    public async Task<short> CreateAsync(CourseGroupRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO CourseGroup (Description)
            VALUES (@Description);
            SELECT CAST(SCOPE_IDENTITY() AS smallint);
            """;

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var parameters = ToParameters(request);
        var pkid = await connection.ExecuteScalarAsync<short>(
            new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));
        await auditWriter.WriteAsync(connection, TableName, pkid.ToString(), RowAuditWriter.Insert,
            parameters.Description, cancellationToken, transaction);

        await transaction.CommitAsync(cancellationToken);
        return pkid;
    }

    public async Task<bool> UpdateAsync(CourseGroupRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE CourseGroup
            SET Description = @Description
            WHERE pkid = @Pkid;
            """;

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var existing = await GetByIdAsync(connection, request.Pkid, cancellationToken, transaction);
        if (existing is null)
        {
            return false;
        }

        var parameters = ToParameters(request);
        var affected = await connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));
        if (affected == 0)
        {
            return false;
        }

        var changed = AuditHelper.ChangedColumns(existing, parameters);
        await auditWriter.WriteAsync(connection, TableName, request.Pkid.ToString(), RowAuditWriter.Update,
            changed.Count == 0 ? "(no changes)" : string.Join(", ", changed), cancellationToken, transaction);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(short pkid, CancellationToken cancellationToken)
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
                "DELETE FROM CourseGroup WHERE pkid = @Pkid", new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));
        }
        catch (SqlException ex) when (ex.Number == SqlForeignKeyViolation)
        {
            throw new EntityInUseException($"課程群組 {pkid}「{existing.Description}」仍被課程或夥伴課程群組使用，無法刪除。");
        }

        await auditWriter.WriteAsync(connection, TableName, pkid.ToString(), RowAuditWriter.Delete,
            existing.Description, cancellationToken, transaction);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<LookupItem>> GetLookupAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<LookupItem>(new CommandDefinition(
            "SELECT CAST(pkid AS int) AS Pkid, Description AS Label FROM CourseGroup ORDER BY Description ASC, pkid ASC",
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    private static Task<CourseGroup?> GetByIdAsync(
        DbConnection connection,
        short pkid,
        CancellationToken cancellationToken,
        DbTransaction? transaction = null)
    {
        return connection.QuerySingleOrDefaultAsync<CourseGroup>(new CommandDefinition(
            SelectColumns + " WHERE pkid = @Pkid", new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>Normalises the request (trim) so stored values and the UPDATE audit diff are consistent.</summary>
    private static CourseGroup ToParameters(CourseGroupRequest request) => new()
    {
        Pkid = request.Pkid,
        Description = request.Description.Trim()
    };
}
