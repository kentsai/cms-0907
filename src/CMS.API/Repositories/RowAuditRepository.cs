using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public sealed class RowAuditRepository(IDbConnectionFactory connectionFactory) : IRowAuditRepository
{
    /// <summary>
    /// <c>PrimaryKeyValues</c> is what <see cref="RowAuditWriter"/> recorded: the row's <c>pkid</c>, or the
    /// <c>[AuditKey]</c> string key for AppRole / AppUser — so <c>pkid</c> here is compared as text, unchanged.
    /// Ties on <c>DateTime</c> fall back to the audit row's own identity so the order is stable.
    /// </summary>
    public const string Sql = """
        SELECT [DateTime], UserName, ActionType, ActionDesc
        FROM RowAudit
        WHERE TableName = @TableName AND PrimaryKeyValues = @Pkid
        ORDER BY [DateTime] DESC, pkid DESC
        """;

    public async Task<IReadOnlyList<RowAudit>> GetForRecordAsync(string tableName, string pkid, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<RowAudit>(new CommandDefinition(
            Sql, new { TableName = tableName, Pkid = pkid }, cancellationToken: cancellationToken));
        return rows.AsList();
    }
}
