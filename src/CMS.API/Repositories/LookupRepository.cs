using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public sealed class LookupRepository(IDbConnectionFactory connectionFactory) : ILookupRepository
{
    public async Task<IReadOnlyList<LookupItem>> GetCertificationsAsync(CancellationToken cancellationToken)
    {
        // Certification.Title is nchar(100) and nullable → RTRIM + ISNULL.
        const string sql = """
            SELECT c.pkid AS Pkid, p.Name + N' ' + ISNULL(RTRIM(c.Title), N'') AS Label
            FROM Certification c
            INNER JOIN Partner p ON p.pkid = c.Partner_pkid
            ORDER BY p.DisplayOrder ASC, p.Name ASC, RTRIM(c.Title) ASC, c.pkid ASC
            """;

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<LookupItem>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<LookupItem>> GetJobCategoriesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CAST(pkid AS int) AS Pkid, Description AS Label
            FROM JobCategory
            ORDER BY Description ASC, pkid ASC
            """;

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<LookupItem>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.AsList();
    }
}
