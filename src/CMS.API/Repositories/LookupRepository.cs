using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public sealed class LookupRepository(IDbConnectionFactory connectionFactory) : ILookupRepository
{
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

    public async Task<IReadOnlyList<LookupItem>> GetTrainingCentersAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CAST(pkid AS int) AS Pkid, Name AS Label
            FROM TrainingCenter
            ORDER BY DisplayOrder ASC, Name ASC, pkid ASC
            """;

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<LookupItem>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<PromotionLookupItem>> SearchPromotionsAsync(string? keyword, int limit, CancellationToken cancellationToken)
    {
        // PromoCodes are date-prefixed (e.g. 20251204_SkillTrainAI), so PromoCode DESC ≈ newest first.
        const string sql = """
            SELECT TOP (@Limit) pkid AS Pkid, PromoCode, Topic, Description
            FROM Promotion2
            WHERE (@Keyword IS NULL OR PromoCode LIKE '%' + @Keyword + '%')
            ORDER BY PromoCode DESC, pkid DESC
            """;

        var parameters = new
        {
            Keyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim(),
            Limit = limit
        };

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PromotionLookupItem>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return rows.AsList();
    }
}
