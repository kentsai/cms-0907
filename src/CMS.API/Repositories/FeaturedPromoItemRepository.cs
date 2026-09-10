using System.Data.Common;
using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CMS.API.Repositories;

public sealed class FeaturedPromoItemRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter auditWriter)
    : IFeaturedPromoItemRepository
{
    private const string TableName = "FeaturedPromoItem";
    private const int SqlUniqueConstraintViolation = 2627;
    private const int SqlUniqueIndexViolation = 2601;

    /// <summary>Parking slot used while two rows swap so the unique (day, centre, slot) index never trips.</summary>
    private const byte TemporarySlot = 0;

    private const string SelectColumns = """
        SELECT f.pkid, f.ScheduleOn, f.TrainingCenter_pkid AS TrainingCenterPkid, f.Slot,
               f.Promotion_pkid AS PromotionPkid, f.Topic, f.Description,
               t.Name AS TrainingCenterName, p.PromoCode
        FROM FeaturedPromoItem f
        INNER JOIN TrainingCenter t ON t.pkid = f.TrainingCenter_pkid
        INNER JOIN Promotion2 p ON p.pkid = f.Promotion_pkid
        """;

    public async Task<IReadOnlyList<FeaturedPromoItem>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<FeaturedPromoItem>(new CommandDefinition(
            SelectColumns + " ORDER BY f.ScheduleOn DESC, f.TrainingCenter_pkid ASC, f.Slot ASC",
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<FeaturedPromoItem>> GetWeekAsync(
        short trainingCenterPkid, DateOnly weekStart, DateOnly weekEnd, CancellationToken cancellationToken)
    {
        const string sql = SelectColumns + """

            WHERE f.TrainingCenter_pkid = @TrainingCenterPkid
              AND f.ScheduleOn >= @WeekStart
              AND f.ScheduleOn <= @WeekEnd
            ORDER BY f.ScheduleOn ASC, f.Slot ASC
            """;

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<FeaturedPromoItem>(new CommandDefinition(
            sql, new { TrainingCenterPkid = trainingCenterPkid, WeekStart = weekStart, WeekEnd = weekEnd },
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<FeaturedPromoItem?> GetByIdAsync(int pkid, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await GetByIdAsync(connection, pkid, cancellationToken);
    }

    public async Task<int> CreateAsync(FeaturedPromoItemRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO FeaturedPromoItem (ScheduleOn, TrainingCenter_pkid, Slot, Promotion_pkid, Topic, Description)
            VALUES (@ScheduleOn, @TrainingCenterPkid, @Slot, @PromotionPkid, @Topic, @Description);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """;

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var parameters = ToParameters(request);
        int pkid;
        try
        {
            pkid = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));
        }
        catch (SqlException ex) when (IsUniqueViolation(ex))
        {
            throw new SlotOccupiedException(SlotOccupiedMessage(parameters.ScheduleOn, parameters.Slot));
        }

        var created = await GetByIdAsync(connection, pkid, cancellationToken, transaction)
            ?? throw new InvalidOperationException($"{TableName} {pkid} was not found after INSERT.");
        await auditWriter.LogInsertAsync(connection, TableName, created, cancellationToken, transaction);

        await transaction.CommitAsync(cancellationToken);
        return pkid;
    }

    public async Task<bool> UpdateAsync(FeaturedPromoItemRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE FeaturedPromoItem
            SET ScheduleOn = @ScheduleOn,
                TrainingCenter_pkid = @TrainingCenterPkid,
                Slot = @Slot,
                Promotion_pkid = @PromotionPkid,
                Topic = @Topic,
                Description = @Description
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
        int affected;
        try
        {
            affected = await connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));
        }
        catch (SqlException ex) when (IsUniqueViolation(ex))
        {
            throw new SlotOccupiedException(SlotOccupiedMessage(parameters.ScheduleOn, parameters.Slot));
        }

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

    public async Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var existing = await GetByIdAsync(connection, pkid, cancellationToken, transaction);
        if (existing is null)
        {
            return false;
        }

        // Nothing references FeaturedPromoItem, so no FK handling is needed.
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM FeaturedPromoItem WHERE pkid = @Pkid", new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));

        await auditWriter.LogDeleteAsync(connection, TableName, existing, cancellationToken, transaction);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SwapSlotAsync(int pkid, byte targetSlot, CancellationToken cancellationToken)
    {
        const string findOccupant = """
            SELECT pkid FROM FeaturedPromoItem
            WHERE ScheduleOn = @ScheduleOn AND TrainingCenter_pkid = @TrainingCenterPkid AND Slot = @Slot
            """;
        const string setSlot = "UPDATE FeaturedPromoItem SET Slot = @Slot WHERE pkid = @Pkid";

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var moving = await GetByIdAsync(connection, pkid, cancellationToken, transaction);
        if (moving is null)
        {
            return false;
        }

        if (moving.Slot == targetSlot)
        {
            return true;
        }

        var occupantPkid = await connection.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            findOccupant,
            new { moving.ScheduleOn, moving.TrainingCenterPkid, Slot = targetSlot },
            transaction, cancellationToken: cancellationToken));

        // Park the moving row on slot 0 first so the unique (day, centre, slot) index is never violated mid-swap.
        await connection.ExecuteAsync(new CommandDefinition(setSlot, new { Slot = TemporarySlot, Pkid = pkid }, transaction, cancellationToken: cancellationToken));

        if (occupantPkid is int other)
        {
            await connection.ExecuteAsync(new CommandDefinition(setSlot, new { moving.Slot, Pkid = other }, transaction, cancellationToken: cancellationToken));
            await auditWriter.WriteAsync(connection, TableName, other.ToString(), RowAuditWriter.Update,
                $"Slot {targetSlot} -> {moving.Slot} (swap with {pkid})", cancellationToken, transaction);
        }

        await connection.ExecuteAsync(new CommandDefinition(setSlot, new { Slot = targetSlot, Pkid = pkid }, transaction, cancellationToken: cancellationToken));
        await auditWriter.WriteAsync(connection, TableName, pkid.ToString(), RowAuditWriter.Update,
            $"Slot {moving.Slot} -> {targetSlot}", cancellationToken, transaction);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static async Task<FeaturedPromoItem?> GetByIdAsync(
        DbConnection connection, int pkid, CancellationToken cancellationToken, DbTransaction? transaction = null)
    {
        return await connection.QuerySingleOrDefaultAsync<FeaturedPromoItem>(new CommandDefinition(
            SelectColumns + " WHERE f.pkid = @Pkid", new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));
    }

    private static bool IsUniqueViolation(SqlException ex) =>
        ex.Number is SqlUniqueConstraintViolation or SqlUniqueIndexViolation;

    private static string SlotOccupiedMessage(DateOnly scheduleOn, byte slot) =>
        $"{scheduleOn:yyyy-MM-dd} 的版位 {slot} 已有資料，無法儲存。";

    /// <summary>Scalar-only parameter object for the INSERT / UPDATE (trimmed text).</summary>
    private static FeaturedPromoItemScalars ToParameters(FeaturedPromoItemRequest request) => new()
    {
        Pkid = request.Pkid,
        ScheduleOn = request.ScheduleOn,
        TrainingCenterPkid = request.TrainingCenterPkid,
        Slot = request.Slot,
        PromotionPkid = request.PromotionPkid,
        Topic = request.Topic.Trim(),
        Description = request.Description.Trim()
    };

    private sealed class FeaturedPromoItemScalars
    {
        public int Pkid { get; init; }
        public DateOnly ScheduleOn { get; init; }
        public short TrainingCenterPkid { get; init; }
        public byte Slot { get; init; }
        public int PromotionPkid { get; init; }
        public string Topic { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
    }
}
