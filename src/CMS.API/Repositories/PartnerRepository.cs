using System.Data.Common;
using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CMS.API.Repositories;

public sealed class PartnerRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter auditWriter)
    : IPartnerRepository
{
    private const string TableName = "Partner";
    private const int SqlForeignKeyViolation = 547;

    private const string SelectColumns = """
        SELECT pkid, Name, AppKey, NameOnPartnerMenu, NameOnCourseDetailPage, DisplayOrder, ImageFilename
        FROM Partner
        """;

    private const string DefaultOrder = " ORDER BY DisplayOrder ASC, pkid DESC";

    public async Task<IReadOnlyList<Partner>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<Partner>(
            new CommandDefinition(SelectColumns + DefaultOrder, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<Partner>> QueryAsync(PartnerQuery query, CancellationToken cancellationToken)
    {
        const string sql = SelectColumns + """

            WHERE (@Keyword IS NULL
                   OR Name LIKE '%' + @Keyword + '%'
                   OR AppKey LIKE '%' + @Keyword + '%'
                   OR NameOnPartnerMenu LIKE '%' + @Keyword + '%'
                   OR NameOnCourseDetailPage LIKE '%' + @Keyword + '%')
            """ + DefaultOrder;

        var parameters = new
        {
            Keyword = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim()
        };

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<Partner>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<Partner?> GetByIdAsync(short pkid, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await GetByIdAsync(connection, pkid, cancellationToken);
    }

    public async Task<short> CreateAsync(PartnerRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO Partner (Name, AppKey, NameOnPartnerMenu, NameOnCourseDetailPage, DisplayOrder, ImageFilename)
            VALUES (@Name, @AppKey, @NameOnPartnerMenu, @NameOnCourseDetailPage, @DisplayOrder, @ImageFilename);
            SELECT CAST(SCOPE_IDENTITY() AS smallint);
            """;

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var pkid = await connection.ExecuteScalarAsync<short>(
            new CommandDefinition(sql, ToParameters(request), transaction, cancellationToken: cancellationToken));
        await auditWriter.WriteAsync(connection, TableName, pkid.ToString(), RowAuditWriter.Insert,
            request.Name, cancellationToken, transaction);

        await transaction.CommitAsync(cancellationToken);
        return pkid;
    }

    public async Task<bool> UpdateAsync(PartnerRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE Partner
            SET Name = @Name,
                AppKey = @AppKey,
                NameOnPartnerMenu = @NameOnPartnerMenu,
                NameOnCourseDetailPage = @NameOnCourseDetailPage,
                DisplayOrder = @DisplayOrder,
                ImageFilename = @ImageFilename
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
                "DELETE FROM Partner WHERE pkid = @Pkid", new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));
        }
        catch (SqlException ex) when (ex.Number == SqlForeignKeyViolation)
        {
            throw new EntityInUseException($"合作夥伴 {pkid}「{existing.Name}」仍被課程、認證、說明會或活動使用，無法刪除。");
        }

        await auditWriter.WriteAsync(connection, TableName, pkid.ToString(), RowAuditWriter.Delete,
            existing.Name, cancellationToken, transaction);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<LookupItem>> GetLookupAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<LookupItem>(new CommandDefinition(
            "SELECT CAST(pkid AS int) AS Pkid, Name AS Label FROM Partner ORDER BY DisplayOrder ASC, Name ASC",
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    private static Task<Partner?> GetByIdAsync(
        DbConnection connection,
        short pkid,
        CancellationToken cancellationToken,
        DbTransaction? transaction = null)
    {
        return connection.QuerySingleOrDefaultAsync<Partner>(new CommandDefinition(
            SelectColumns + " WHERE pkid = @Pkid", new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Normalises the request before it hits SQL (trim strings, blank ImageFilename → NULL) so the
    /// stored values and the UPDATE audit diff are consistent.
    /// </summary>
    private static Partner ToParameters(PartnerRequest request) => new()
    {
        Pkid = request.Pkid,
        Name = request.Name.Trim(),
        AppKey = request.AppKey.Trim(),
        NameOnPartnerMenu = request.NameOnPartnerMenu.Trim(),
        NameOnCourseDetailPage = request.NameOnCourseDetailPage.Trim(),
        DisplayOrder = request.DisplayOrder,
        ImageFilename = string.IsNullOrWhiteSpace(request.ImageFilename) ? null : request.ImageFilename.Trim()
    };
}
