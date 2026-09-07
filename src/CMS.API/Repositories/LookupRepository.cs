using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public sealed class LookupRepository(IDbConnectionFactory connectionFactory) : ILookupRepository
{
    public async Task<IReadOnlyList<StringLookupItem>> GetAppUsersAsync(CancellationToken cancellationToken)
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
}
