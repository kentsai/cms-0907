using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public sealed class RowAuditRepository(IDbConnectionFactory connectionFactory) : IRowAuditRepository
{
    public async Task<IReadOnlyList<RowAudit>> GetForRowAsync(
        string tableName, string primaryKeyValues, int take, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (@Take) pkid, TableName, UserName, PrimaryKeyValues, ActionType, ActionDesc, [DateTime]
            FROM RowAudit
            WHERE TableName = @TableName AND PrimaryKeyValues = @PrimaryKeyValues
            ORDER BY [DateTime] DESC, pkid DESC
            """;

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<RowAudit>(new CommandDefinition(
            sql, new { Take = take, TableName = tableName, PrimaryKeyValues = primaryKeyValues },
            cancellationToken: cancellationToken));
        return rows.AsList();
    }
}
